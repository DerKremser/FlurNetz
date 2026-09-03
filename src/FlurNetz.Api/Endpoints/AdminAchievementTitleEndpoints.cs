using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using FlurNetz.BuildingBlocks.Time;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

public static class AdminAchievementTitleEndpoints
{
    public static IEndpointRouteBuilder MapAdminAchievementTitleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/admin/achievements/definitions", ListAchievementDefinitionsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsRead));
        endpoints.MapPost("/api/admin/achievements/definitions", CreateAchievementDefinitionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapPut("/api/admin/achievements/definitions/{definitionId}/display-name", RenameAchievementDefinitionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapPut("/api/admin/achievements/definitions/{definitionId}/description", ChangeAchievementDescriptionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/achievements", ListCommunityAchievementsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsRead));
        endpoints.MapPost("/api/admin/achievements/{communityIdentityId}/{definitionId}/unlock", UnlockAchievementAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.AchievementsUnlock))
            .RequireAntiforgery();

        endpoints.MapGet("/api/admin/titles/definitions", ListTitleDefinitionsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesRead));
        endpoints.MapPost("/api/admin/titles/definitions", CreateTitleDefinitionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapPut("/api/admin/titles/definitions/{definitionId}/display-name", RenameTitleDefinitionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapPut("/api/admin/titles/definitions/{definitionId}/description", ChangeTitleDescriptionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesManageDefinitions))
            .RequireAntiforgery();
        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/titles", ReadCommunityTitlesAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesRead));
        endpoints.MapPost("/api/admin/titles/{communityIdentityId}/{definitionId}/unlock", UnlockTitleAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesUnlock))
            .RequireAntiforgery();
        endpoints.MapPost("/api/admin/titles/{communityIdentityId}/{definitionId}/lock", LockTitleAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.TitlesLock))
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListAchievementDefinitionsAsync(ListAchievementDefinitions reader, CancellationToken token) =>
        Results.Ok((await reader.ExecuteAsync(token).ConfigureAwait(false)).Select(ToResponse).ToArray());

    private static async Task<IResult> CreateAchievementDefinitionAsync([FromBody] AdminDefinitionRequest? request, IAchievementDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (request is null) return Invalid("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var definition = AchievementDefinition.Create(AchievementDefinitionId.New(), request.DisplayName!, request.Description);
            await coordinator.ExecuteAuditedAsync((connection, transaction, cancellationToken) => store.AddAsync(definition, connection, transaction, cancellationToken), () => AuditNormal(context, AdminAuditActions.DefinitionCreated, "AchievementDefinition", definition.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }), token).ConfigureAwait(false);
            return Results.Created("/api/admin/achievements/definitions", ToResponse(definition));
        }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> RenameAchievementDefinitionAsync(string definitionId, [FromBody] AdminDefinitionRequest? request, IAchievementDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (!TryDefinition(definitionId, out var id) || request is null) return Invalid("Definition-ID oder Request-Body ist ungültig.");
        try { return await MutateDefinitionAsync(id, definition => definition.Rename(request.DisplayName!), AdminAuditActions.DefinitionUpdated, store, coordinator, contextAccessor, token).ConfigureAwait(false); }
        catch (AchievementDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ChangeAchievementDescriptionAsync(string definitionId, [FromBody] AdminDefinitionRequest? request, IAchievementDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (!TryDefinition(definitionId, out var id) || request is null) return Invalid("Definition-ID oder Request-Body ist ungültig.");
        try { return await MutateDefinitionAsync(id, definition => definition.ChangeDescription(request.Description), AdminAuditActions.DefinitionUpdated, store, coordinator, contextAccessor, token).ConfigureAwait(false); }
        catch (AchievementDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ListCommunityAchievementsAsync(string communityIdentityId, ListCommunityAchievements reader, CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Identity-ID ist ungültig.");
        var entries = await reader.ExecuteAsync(identity, token).ConfigureAwait(false);
        return Results.Ok(entries.Select(entry => new AdminAchievementResponse(entry.AchievementDefinitionId.Value, entry.UnlockedAtUtc)).ToArray());
    }

    private static async Task<IResult> UnlockAchievementAsync(
        string communityIdentityId,
        string definitionId,
        [FromBody] AdminCommunityDefinitionActionRequest? request,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        IAchievementDefinitionStore definitionStore,
        ICommunityAchievementStore achievementStore,
        IClock clock,
        AdminMutationCoordinator coordinator,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity) || !TryDefinition(definitionId, out var definitionIdValue)) return Invalid("Identity- oder Definition-ID ist ungültig.");
        if (!TryReasonAndRequest(request?.Reason, request?.RequestId, out var reason, out var requestId, out var error)) return Invalid(error!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            var result = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(requestId, context.ActorCommunityIdentityId, "Achievements.Unlock", "CommunityIdentity", identity.Value.ToString("D"), AdminRequestFingerprint.Compute(("identity", identity.Value), ("definition", definitionIdValue.Value), ("reason", reason)), context.CorrelationId, DateTimeOffset.UtcNow),
                    async (connection, transaction, cancellationToken) =>
                    {
                        if (!await identityExistence.ExistsAsync(identity, connection, transaction, cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException();
                        await definitionStore.ExecuteAsync(definitionIdValue, definition =>
                        {
                            var achievement = CommunityAchievement.Create(identity, definition.Id, clock.UtcNow);
                            return achievementStore.UnlockAsync(achievement, connection, transaction, cancellationToken);
                        }, connection, transaction, cancellationToken).ConfigureAwait(false);
                    },
                    () => Audit(context, AdminAuditActions.AchievementUnlocked, identity.Value.ToString("D"), reason, requestId, new Dictionary<string, string?> { ["AchievementDefinitionId"] = definitionIdValue.Value.ToString("D"), ["Unlocked"] = "true" }),
                    token).ConfigureAwait(false);
            return Results.Ok(new AdminAlreadyCompletedResponse(result.AlreadyCompleted));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (AchievementDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ListTitleDefinitionsAsync(ListTitleDefinitions reader, CancellationToken token) =>
        Results.Ok((await reader.ExecuteAsync(token).ConfigureAwait(false)).Select(definition => new AdminTitleDefinitionResponse(definition.Id.Value, definition.DisplayName, definition.Description)).ToArray());

    private static async Task<IResult> CreateTitleDefinitionAsync([FromBody] AdminDefinitionRequest? request, ITitleDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (request is null) return Invalid("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var definition = TitleDefinition.Create(TitleDefinitionId.New(), request.DisplayName!, request.Description);
            await coordinator.ExecuteAuditedAsync((connection, transaction, cancellationToken) => store.AddAsync(definition, connection, transaction, cancellationToken), () => AuditNormal(context, AdminAuditActions.TitleDefinitionCreated, "TitleDefinition", definition.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }), token).ConfigureAwait(false);
            return Results.Created($"/api/admin/titles/definitions/{definition.Id.Value:D}", new AdminTitleDefinitionResponse(definition.Id.Value, definition.DisplayName, definition.Description));
        }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> RenameTitleDefinitionAsync(string definitionId, [FromBody] AdminDefinitionRequest? request, ITitleDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (!TryTitleDefinition(definitionId, out var id) || request is null) return Invalid("Definition-ID oder Request-Body ist ungültig.");
        try { return await MutateTitleDefinitionAsync(id, definition => definition.Rename(request.DisplayName!), store, coordinator, contextAccessor, token).ConfigureAwait(false); }
        catch (TitleDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ChangeTitleDescriptionAsync(string definitionId, [FromBody] AdminDefinitionRequest? request, ITitleDefinitionStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (!TryTitleDefinition(definitionId, out var id) || request is null) return Invalid("Definition-ID oder Request-Body ist ungültig.");
        try { return await MutateTitleDefinitionAsync(id, definition => definition.ChangeDescription(request.Description), store, coordinator, contextAccessor, token).ConfigureAwait(false); }
        catch (TitleDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ReadCommunityTitlesAsync(string communityIdentityId, ICommunityTitlesStore store, CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Identity-ID ist ungültig.");
        var snapshot = await store.ExecuteAsync(identity, titles => new AdminTitlesResponse(identity.Value, titles.UnlockedTitleDefinitionIds.Select(id => id.Value).ToArray(), titles.CurrentTitleDefinitionId?.Value), token).ConfigureAwait(false);
        return Results.Ok(snapshot);
    }

    private static Task<IResult> UnlockTitleAsync(string communityIdentityId, string definitionId, [FromBody] AdminCommunityDefinitionActionRequest? request, ICommunityTitlesStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token) =>
        ChangeTitleAsync(communityIdentityId, definitionId, request, true, store, coordinator, contextAccessor, token);

    private static Task<IResult> LockTitleAsync(string communityIdentityId, string definitionId, [FromBody] AdminCommunityDefinitionActionRequest? request, ICommunityTitlesStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token) =>
        ChangeTitleAsync(communityIdentityId, definitionId, request, false, store, coordinator, contextAccessor, token);

    private static async Task<IResult> ChangeTitleAsync(string communityIdentityId, string definitionId, AdminCommunityDefinitionActionRequest? request, bool unlock, ICommunityTitlesStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity) || !TryTitleDefinition(definitionId, out var titleId)) return Invalid("Identity- oder Definition-ID ist ungültig.");
        if (!TryReasonAndRequest(request?.Reason, request?.RequestId, out var reason, out var requestId, out var error)) return Invalid(error!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            var action = unlock ? AdminAuditActions.TitleUnlocked : AdminAuditActions.TitleLocked;
            var changed = false;
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestId,
                        context.ActorCommunityIdentityId,
                        action,
                        "CommunityIdentity",
                        identity.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("identity", identity.Value), ("definition", titleId.Value), ("operation", action), ("reason", reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    (connection, transaction, cancellationToken) => store.ExecuteAsync(
                        identity,
                        titles => changed = unlock ? titles.Unlock(titleId) : titles.Lock(titleId),
                        connection,
                        transaction,
                        cancellationToken),
                    () => Audit(context, action, identity.Value.ToString("D"), reason, requestId, new Dictionary<string, string?> { ["TitleDefinitionId"] = titleId.Value.ToString("D"), ["Changed"] = changed.ToString().ToLowerInvariant() }),
                    token)
                .ConfigureAwait(false);
            return Results.Ok(new AdminChangedResponse(mutation.AlreadyCompleted ? true : changed));
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (TitleDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static AdminAuditEntry Audit(AdminExecutionContext context, string action, string targetId, string reason, Guid requestId, IReadOnlyDictionary<string, string?> summary) =>
        new(Guid.NewGuid(), context.ActorCommunityIdentityId, context.ActorLoginName, action, "CommunityIdentity", targetId, null, AdminRiskLevel.High, reason, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow, context.CorrelationId, requestId, null, summary, new Dictionary<string, string?>());

    private static async Task<IResult> MutateDefinitionAsync(
        AchievementDefinitionId id,
        Func<AchievementDefinition, bool> mutation,
        string action,
        IAchievementDefinitionStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken token)
    {
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        await coordinator.ExecuteAuditedAsync(
            (connection, transaction, cancellationToken) => store.ExecuteAsync(id, mutation, connection, transaction, cancellationToken),
            () => AuditNormal(context, action, "AchievementDefinition", id.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
            token).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> MutateTitleDefinitionAsync(
        TitleDefinitionId id,
        Func<TitleDefinition, bool> mutation,
        ITitleDefinitionStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken token)
    {
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        await coordinator.ExecuteAuditedAsync(
            (connection, transaction, cancellationToken) => store.ExecuteAsync(id, mutation, connection, transaction, cancellationToken),
            () => AuditNormal(context, AdminAuditActions.TitleDefinitionUpdated, "TitleDefinition", id.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
            token).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static AdminAuditEntry AuditNormal(AdminExecutionContext context, string action, string targetType, string targetId, IReadOnlyDictionary<string, string?> summary) =>
        new(Guid.NewGuid(), context.ActorCommunityIdentityId, context.ActorLoginName, action, targetType, targetId, null, AdminRiskLevel.Medium, null, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow, context.CorrelationId, null, null, summary, new Dictionary<string, string?>());

    private static bool TryReasonAndRequest(string? rawReason, Guid? rawRequestId, out string reason, out Guid requestId, out string? error)
    {
        try { reason = AdminReason.Required(rawReason); }
        catch (ArgumentException exception) { reason = string.Empty; requestId = Guid.Empty; error = exception.Message; return false; }
        if (rawRequestId is not Guid value || value == Guid.Empty) { requestId = Guid.Empty; error = "Eine eindeutige RequestId ist erforderlich."; return false; }
        requestId = value; error = null; return true;
    }

    private static bool TryIdentity(string raw, out CommunityIdentityId id) => TryId(raw, CommunityIdentityId.Create, out id);
    private static bool TryDefinition(string raw, out AchievementDefinitionId id) => TryId(raw, AchievementDefinitionId.Create, out id);
    private static bool TryTitleDefinition(string raw, out TitleDefinitionId id) => TryId(raw, TitleDefinitionId.Create, out id);
    private static bool TryId<T>(string raw, Func<Guid, T> create, out T id)
    {
        id = default!;
        if (!Guid.TryParse(raw, out var value) || value == Guid.Empty) return false;
        try { id = create(value); return true; } catch (ArgumentException) { return false; }
    }

    private static AdminAchievementDefinitionResponse ToResponse(AchievementDefinition definition) => new(definition.Id.Value, definition.DisplayName, definition.Description);
    private static IResult Invalid(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
}
