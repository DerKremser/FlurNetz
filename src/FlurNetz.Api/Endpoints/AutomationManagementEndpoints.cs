using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.BuildingBlocks.Time;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

/// <summary>Ordnet die interne Automation-Management-API zu.</summary>
public static class AutomationManagementEndpoints
{
    private const string RulesRoute = "/api/admin/automation/rules";

    /// <summary>Registriert alle V1-Management-Endpunkte ohne Delete, Run-Now oder Dry-Run.</summary>
    public static IEndpointRouteBuilder MapAutomationManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(RulesRoute, ListAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationRead));
        endpoints.MapGet($"{RulesRoute}/{{ruleId}}", GetAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationRead));
        endpoints.MapPost(RulesRoute, CreateAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationManage))
            .RequireAntiforgery();
        endpoints.MapPut($"{RulesRoute}/{{ruleId}}", ReplaceAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/enable", EnableAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/disable", DisableAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/archive", ArchiveAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationManage))
            .RequireAntiforgery();
        endpoints.MapGet($"{RulesRoute}/{{ruleId}}/executions", ListExecutionsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AutomationRead));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ListAutomationRules useCase, CancellationToken cancellationToken)
    {
        var rules = await useCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(new AutomationRuleListResponse(rules.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> GetAsync(string ruleId, GetAutomationRule useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(ruleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        var rule = await useCase.ExecuteAsync(validId, cancellationToken).ConfigureAwait(false);
        return rule is null
            ? NotFound(validId)
            : Results.Ok(ToResponse(rule));
    }

    private static async Task<IResult> CreateAsync([FromBody] AutomationRuleRequest? request, IAutomationRuleStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken)
    {
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var rule = AutomationRule.Create(AutomationRuleId.New(), request.DisplayName!, request.Description, request.TriggerType!, ToConditions(request.Conditions), ToActions(request.Actions), request.SortOrder ?? 0, Canonicalize(clock.UtcNow));
            await coordinator.ExecuteAuditedAsync(
                (connection, transaction, token) => store.AddAsync(rule, connection, transaction, token),
                () => NormalAudit(context, AdminAuditActions.RuleCreated, rule.AutomationRuleId.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return Results.Created($"{RulesRoute}/{rule.AutomationRuleId.Value}", ToResponse(rule));
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> ReplaceAsync(string ruleId, [FromBody] AutomationRuleRequest? request, IAutomationRuleStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken)
    {
        if (!TryCreateId(ruleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            await coordinator.ExecuteAuditedAsync(
                async (connection, transaction, token) =>
                {
                    var rule = await store.MutateAsync(validId, value =>
                    {
                        if (value.IsEnabled || value.IsArchived) throw new AutomationRuleConflictException(validId, "Eine aktive oder archivierte Automation-Rule kann nicht ersetzt werden.");
                        return value.ReplaceConfiguration(request.DisplayName!, request.Description, request.TriggerType!, ToConditions(request.Conditions), ToActions(request.Actions), request.SortOrder ?? 0, Canonicalize(clock.UtcNow));
                    }, connection, transaction, token).ConfigureAwait(false);
                    if (rule is null) throw new AutomationRuleNotFoundException(validId);
                },
                () => NormalAudit(context, AdminAuditActions.RuleUpdated, validId.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (AutomationRuleNotFoundException exception) { return NotFound(exception.RuleId); }
        catch (AutomationRuleConflictException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static Task<IResult> EnableAsync(string ruleId, IAutomationRuleStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken) =>
        StatusAsync(ruleId, value => value.Enable(Canonicalize(clock.UtcNow)), AdminAuditActions.RuleEnabled, store, coordinator, contextAccessor, cancellationToken);

    private static Task<IResult> DisableAsync(string ruleId, IAutomationRuleStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken) =>
        StatusAsync(ruleId, value => value.Disable(Canonicalize(clock.UtcNow)), AdminAuditActions.RuleDisabled, store, coordinator, contextAccessor, cancellationToken);

    private static async Task<IResult> ArchiveAsync(
        string ruleId,
        [FromBody] AdminActionRequest? request,
        IAutomationRuleStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(ruleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        if (!TryHighRiskRequest(request, out var requestData, out var requestError)) return InvalidRequest(requestError!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();

        try
        {
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.RuleArchived,
                        "AutomationRule",
                        validId.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("rule", validId.Value), ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, token) =>
                    {
                        var rule = await store.MutateAsync(
                                validId,
                                value => value.Archive(Canonicalize(clock.UtcNow)),
                                connection,
                                transaction,
                                token)
                            .ConfigureAwait(false);
                        if (rule is null) throw new AutomationRuleNotFoundException(validId);
                    },
                    () => CreateAudit(
                        context,
                        AdminAuditActions.RuleArchived,
                        validId.Value.ToString("D"),
                        requestData.Reason,
                        requestData.RequestId,
                        new Dictionary<string, string?> { ["Archived"] = "true" }),
                    cancellationToken)
                .ConfigureAwait(false);

            return mutation.AlreadyCompleted
                ? Results.Ok(new AdminAlreadyCompletedResponse(true))
                : Results.NoContent();
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (AutomationRuleNotFoundException exception) { return NotFound(exception.RuleId); }
        catch (AutomationRuleArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> StatusAsync(string rawRuleId, Func<AutomationRule, bool> mutation, string action, IAutomationRuleStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawRuleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            await coordinator.ExecuteAuditedAsync(
                async (connection, transaction, token) =>
                {
                    var rule = await store.MutateAsync(validId, mutation, connection, transaction, token).ConfigureAwait(false);
                    if (rule is null) throw new AutomationRuleNotFoundException(validId);
                },
                () => NormalAudit(context, action, validId.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (AutomationRuleNotFoundException exception) { return NotFound(exception.RuleId); }
        catch (AutomationRuleArchivedException exception) { return Conflict(exception.Message); }
        catch (AutomationRuleConflictException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static AdminAuditEntry NormalAudit(AdminExecutionContext context, string action, string targetId, IReadOnlyDictionary<string, string?> summary) =>
        new(Guid.NewGuid(), context.ActorCommunityIdentityId, context.ActorCommunityIdentityId.Value.ToString("D"), action, "AutomationRule", targetId, null, AdminRiskLevel.Medium, null, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow, context.CorrelationId, null, null, summary, new Dictionary<string, string?>());

    private static bool TryHighRiskRequest(AdminActionRequest? request, out (Guid RequestId, string Reason) value, out string? error)
    {
        value = default;
        try
        {
            if (request?.RequestId is not Guid requestId || requestId == Guid.Empty) throw new ArgumentException("Eine eindeutige RequestId ist erforderlich.");
            value = (requestId, AdminReason.Required(request.Reason));
            error = null;
            return true;
        }
        catch (ArgumentException exception) { error = exception.Message; return false; }
    }

    private static AdminAuditEntry CreateAudit(
        AdminExecutionContext context,
        string action,
        string targetId,
        string reason,
        Guid requestId,
        IReadOnlyDictionary<string, string?> changeSummary) =>
        new(
            Guid.NewGuid(),
            context.ActorCommunityIdentityId,
            context.ActorCommunityIdentityId.Value.ToString("D"),
            action,
            "AutomationRule",
            targetId,
            null,
            AdminRiskLevel.High,
            reason,
            AdminAuditOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            context.CorrelationId,
            requestId,
            null,
            changeSummary,
            new Dictionary<string, string?>());

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static async Task<IResult> ListExecutionsAsync(
        string ruleId,
        string? cursor,
        int? pageSize,
        GetAutomationRule getRule,
        ListAutomationExecutions useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(ruleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        if (await getRule.ExecuteAsync(validId, cancellationToken).ConfigureAwait(false) is null) return NotFound(validId);
        if (pageSize is < ListAutomationExecutions.MinimumPageSize or > ListAutomationExecutions.MaximumPageSize)
        {
            return InvalidRequest($"Die Seitengröße muss zwischen {ListAutomationExecutions.MinimumPageSize} und {ListAutomationExecutions.MaximumPageSize} liegen.");
        }

        AutomationExecutionCursor? decoded = null;
        if (cursor is not null && !AutomationExecutionCursorCodec.TryDecode(cursor, validId, out decoded, out var error))
        {
            return InvalidRequest(error);
        }

        try
        {
            var page = await useCase.ExecuteAsync(validId, decoded, pageSize ?? ListAutomationExecutions.DefaultPageSize, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new AutomationExecutionPageResponse(
                page.Items.Select(ToResponse).ToArray(),
                page.NextCursor is null ? null : AutomationExecutionCursorCodec.Encode(page.NextCursor)));
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static IReadOnlyList<AutomationCondition> ToConditions(IReadOnlyList<AutomationConditionRequest>? requests)
    {
        if (requests is null) return Array.Empty<AutomationCondition>();
        return requests.Select((request, index) =>
        {
            if (request is null) throw new ArgumentException("Eine Condition darf nicht null sein.", nameof(requests));
            return AutomationCondition.Create(index, request.Type!, request.CommunityIdentityId, request.ShopOfferId, request.ItemDefinitionId, request.Amount);
        }).ToArray();
    }

    private static IReadOnlyList<AutomationAction> ToActions(IReadOnlyList<AutomationActionRequest>? requests)
    {
        if (requests is null) return Array.Empty<AutomationAction>();
        return requests.Select((request, index) =>
        {
            if (request is null) throw new ArgumentException("Eine Action darf nicht null sein.", nameof(requests));
            OverlayChannelId? channelId = request.OverlayChannelId is Guid value ? OverlayChannelId.Create(value) : null;
            return AutomationAction.Create(index, request.Type!, request.Amount, request.Title, request.Message, channelId, request.Variant, request.DurationMilliseconds);
        }).ToArray();
    }

    private static AutomationRuleResponse ToResponse(AutomationRule rule) =>
        new(
            rule.AutomationRuleId.Value,
            rule.DisplayName,
            rule.Description,
            rule.TriggerType,
            rule.Conditions.Select(condition => new AutomationConditionResponse(condition.Position, condition.ConditionType, condition.CommunityIdentityId, condition.ShopOfferId, condition.ItemDefinitionId, condition.Amount)).ToArray(),
            rule.Actions.Select(action => new AutomationActionResponse(action.Position, action.ActionType, action.Amount, action.Title, action.Message, action.OverlayChannelId?.Value, action.Variant, action.DurationMilliseconds)).ToArray(),
            rule.SortOrder,
            rule.IsEnabled,
            rule.IsArchived,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);

    private static AutomationExecutionResponse ToResponse(AutomationExecution execution) =>
        new(
            execution.Id.Value,
            execution.AutomationRuleId.Value,
            execution.TriggerMessageId,
            execution.TriggerMessageType,
            execution.TriggerSchemaVersion,
            execution.CommunityIdentityId,
            execution.TriggerOccurredAtUtc,
            execution.ExecutedAtUtc);

    private static bool TryCreateId(string raw, out AutomationRuleId id)
    {
        id = default;
        if (!Guid.TryParse(raw, out var value) || value == Guid.Empty) return false;
        try { id = AutomationRuleId.Create(value); return true; }
        catch (ArgumentException) { return false; }
    }

    private static IResult InvalidRequest(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
    private static IResult NotFound(AutomationRuleId id) => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Automation-Rule nicht gefunden.", detail: $"Die Automation-Rule '{id.Value}' wurde nicht gefunden.");
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Automation-Rule-Konflikt.", detail: detail);
}
