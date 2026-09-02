using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Api.Endpoints;

/// <summary>Ordnet die interne Automation-Management-API zu.</summary>
public static class AutomationManagementEndpoints
{
    private const string RulesRoute = "/api/admin/automation/rules";

    /// <summary>Registriert alle V1-Management-Endpunkte ohne Delete, Run-Now oder Dry-Run.</summary>
    public static IEndpointRouteBuilder MapAutomationManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(RulesRoute, ListAsync);
        endpoints.MapGet($"{RulesRoute}/{{ruleId}}", GetAsync);
        endpoints.MapPost(RulesRoute, CreateAsync);
        endpoints.MapPut($"{RulesRoute}/{{ruleId}}", ReplaceAsync);
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/enable", EnableAsync);
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/disable", DisableAsync);
        endpoints.MapPost($"{RulesRoute}/{{ruleId}}/archive", ArchiveAsync);
        endpoints.MapGet($"{RulesRoute}/{{ruleId}}/executions", ListExecutionsAsync);
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

    private static async Task<IResult> CreateAsync(AutomationRuleRequest? request, CreateAutomationRule useCase, CancellationToken cancellationToken)
    {
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var rule = await useCase.ExecuteAsync(
                request.DisplayName!,
                request.Description,
                request.TriggerType!,
                ToConditions(request.Conditions),
                ToActions(request.Actions),
                request.SortOrder ?? 0,
                cancellationToken).ConfigureAwait(false);
            return Results.Created($"{RulesRoute}/{rule.AutomationRuleId.Value}", ToResponse(rule));
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> ReplaceAsync(string ruleId, AutomationRuleRequest? request, ReplaceAutomationRule useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(ruleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            await useCase.ExecuteAsync(
                validId,
                request.DisplayName!,
                request.Description,
                request.TriggerType!,
                ToConditions(request.Conditions),
                ToActions(request.Actions),
                request.SortOrder ?? 0,
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (AutomationRuleNotFoundException exception) { return NotFound(exception.RuleId); }
        catch (AutomationRuleConflictException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static Task<IResult> EnableAsync(string ruleId, EnableAutomationRule useCase, CancellationToken cancellationToken) =>
        StatusAsync(ruleId, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> DisableAsync(string ruleId, DisableAutomationRule useCase, CancellationToken cancellationToken) =>
        StatusAsync(ruleId, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> ArchiveAsync(string ruleId, ArchiveAutomationRule useCase, CancellationToken cancellationToken) =>
        StatusAsync(ruleId, useCase.ExecuteAsync, cancellationToken);

    private static async Task<IResult> StatusAsync(string rawRuleId, Func<AutomationRuleId, CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawRuleId, out var validId)) return InvalidRequest("Die Route-ID der Automation-Rule ist ungültig.");
        try
        {
            await operation(validId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (AutomationRuleNotFoundException exception) { return NotFound(exception.RuleId); }
        catch (AutomationRuleArchivedException exception) { return Conflict(exception.Message); }
        catch (AutomationRuleConflictException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
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
            return AutomationAction.Create(index, request.Type!, request.Amount, request.Title, request.Message);
        }).ToArray();
    }

    private static AutomationRuleResponse ToResponse(AutomationRule rule) =>
        new(
            rule.AutomationRuleId.Value,
            rule.DisplayName,
            rule.Description,
            rule.TriggerType,
            rule.Conditions.Select(condition => new AutomationConditionResponse(condition.Position, condition.ConditionType, condition.CommunityIdentityId, condition.ShopOfferId, condition.ItemDefinitionId, condition.Amount)).ToArray(),
            rule.Actions.Select(action => new AutomationActionResponse(action.Position, action.ActionType, action.Amount, action.Title, action.Message)).ToArray(),
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
