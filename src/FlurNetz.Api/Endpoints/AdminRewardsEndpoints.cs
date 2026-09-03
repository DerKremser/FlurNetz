using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

public static class AdminRewardsEndpoints
{
    private const string SourceType = "administration.manual-grant";

    public static IEndpointRouteBuilder MapAdminRewardsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/admin/rewards/definitions", ListDefinitionsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsRead));
        endpoints.MapPost("/api/admin/rewards/definitions/economy", CreateDefinitionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsCreate))
            .RequireAntiforgery();
        endpoints.MapGet("/api/admin/rewards/packages", ListPackagesAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsRead));
        endpoints.MapPost("/api/admin/rewards/packages", CreatePackageAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsCreate))
            .RequireAntiforgery();
        endpoints.MapGet("/api/admin/rewards/grants", ListGrantsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsRead));
        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/rewards/grants", ListIdentityGrantsAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsRead));
        endpoints.MapPost("/api/admin/rewards/grant/{communityIdentityId}", GrantPackageAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.RewardsGrant))
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListDefinitionsAsync(IRewardCatalogStore store, CancellationToken token) =>
        Results.Ok((await store.ListDefinitionsAsync(token).ConfigureAwait(false)).Select(definition => definition switch
        {
            EconomyBalanceRewardDefinition economy => new AdminRewardDefinitionResponse(economy.Id.Value, "economy-balance", economy.Amount),
            _ => new AdminRewardDefinitionResponse(definition.Id.Value, "unknown", 0)
        }).ToArray());

    private static async Task<IResult> CreateDefinitionAsync([FromBody] AdminRewardDefinitionRequest? request, IRewardCatalogStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (request?.Amount is not long amount || amount <= 0) return Invalid("Der Reward-Betrag muss positiv sein.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var definition = EconomyBalanceRewardDefinition.Create(RewardDefinitionId.New(), amount);
            await coordinator.ExecuteAuditedAsync((connection, transaction, cancellationToken) => store.AddDefinitionAsync(definition, connection, transaction, cancellationToken), () => NormalAudit(context, AdminAuditActions.RewardDefinitionCreated, "RewardDefinition", definition.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }), token).ConfigureAwait(false);
            return Results.Created($"/api/admin/rewards/definitions/{definition.Id.Value:D}", new AdminRewardDefinitionResponse(definition.Id.Value, "economy-balance", amount));
        }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ListPackagesAsync(IRewardCatalogStore store, CancellationToken token) =>
        Results.Ok((await store.ListPackagesAsync(token).ConfigureAwait(false)).Select(package => new AdminRewardPackageResponse(package.Id.Value, package.RewardDefinitionIds.Select(id => id.Value).ToArray())).ToArray());

    private static async Task<IResult> CreatePackageAsync([FromBody] AdminRewardPackageRequest? request, IRewardCatalogStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken token)
    {
        if (request?.DefinitionIds is null || request.DefinitionIds.Count == 0 || request.DefinitionIds.Any(id => id == Guid.Empty)) return Invalid("Mindestens eine gültige Reward-Definition ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var package = RewardPackage.Create(RewardPackageId.New(), request.DefinitionIds.Select(RewardDefinitionId.Create));
            await coordinator.ExecuteAuditedAsync((connection, transaction, cancellationToken) => store.AddPackageAsync(package, connection, transaction, cancellationToken), () => NormalAudit(context, AdminAuditActions.RewardPackageCreated, "RewardPackage", package.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }), token).ConfigureAwait(false);
            return Results.Created($"/api/admin/rewards/packages/{package.Id.Value:D}", new AdminRewardPackageResponse(package.Id.Value, package.RewardDefinitionIds.Select(id => id.Value).ToArray()));
        }
        catch (RewardDefinitionNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ListGrantsAsync(IRewardCatalogStore store, CancellationToken token) => Results.Ok((await store.ListGrantsAsync(null, token).ConfigureAwait(false)).Select(ToResponse).ToArray());

    private static async Task<IResult> ListIdentityGrantsAsync(string communityIdentityId, IRewardCatalogStore store, CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Identity-ID ist ungültig.");
        return Results.Ok((await store.ListGrantsAsync(identity, token).ConfigureAwait(false)).Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GrantPackageAsync(
        string communityIdentityId,
        [FromBody] AdminRewardGrantRequest? request,
        IRewardPackageGrantExecutor executor,
        ICommunityIdentityExistence identityExistence,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Identity-ID ist ungültig.");
        if (request?.PackageId is not Guid packageId || packageId == Guid.Empty) return Invalid("Package-ID ist erforderlich.");
        if (!TryReasonAndRequest(request.Reason, request.RequestId, out var reason, out var requestId, out var error)) return Invalid(error!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            var outcome = RewardPackageGrantOutcome.AlreadyGranted;
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.RewardPackageGranted,
                        "CommunityIdentity",
                        identity.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("identity", identity.Value), ("package", packageId), ("reason", reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, cancellationToken) =>
                    {
                        if (!await identityExistence.ExistsAsync(identity, connection, transaction, cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException();
                        outcome = await executor.ExecuteAsync(
                                RewardPackageId.Create(packageId),
                                identity,
                                RewardSource.Create(SourceType, requestId.ToString("D")),
                                connection,
                                transaction,
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    () => new AdminAuditEntry(
                        Guid.NewGuid(),
                        context.ActorCommunityIdentityId,
                        context.ActorCommunityIdentityId.Value.ToString("D"),
                        AdminAuditActions.RewardPackageGranted,
                        "CommunityIdentity",
                        identity.Value.ToString("D"),
                        null,
                        AdminRiskLevel.High,
                        reason,
                        AdminAuditOutcome.Succeeded,
                        DateTimeOffset.UtcNow,
                        context.CorrelationId,
                        requestId,
                        null,
                        new Dictionary<string, string?>
                        {
                            ["RewardPackageId"] = packageId.ToString("D"),
                            ["Granted"] = (outcome == RewardPackageGrantOutcome.Granted).ToString().ToLowerInvariant()
                        },
                        new Dictionary<string, string?>()),
                    token)
                .ConfigureAwait(false);
            if (mutation.AlreadyCompleted) return Results.Ok(new AdminRewardGrantStatusResponse(false, true));
            return Results.Ok(new AdminRewardGrantStatusResponse(outcome == RewardPackageGrantOutcome.Granted, outcome == RewardPackageGrantOutcome.AlreadyGranted));
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (KeyNotFoundException exception) { return Results.NotFound(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static AdminRewardGrantResponse ToResponse(RewardGrant grant) => new(grant.Id.Value, grant.CommunityIdentityId.Value, grant.RewardDefinitionId.Value, grant.Source.SourceType, grant.Source.SourceId);

    private static AdminAuditEntry NormalAudit(AdminExecutionContext context, string action, string targetType, string targetId, IReadOnlyDictionary<string, string?> summary) =>
        new(Guid.NewGuid(), context.ActorCommunityIdentityId, context.ActorCommunityIdentityId.Value.ToString("D"), action, targetType, targetId, null, AdminRiskLevel.Medium, null, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow, context.CorrelationId, null, null, summary, new Dictionary<string, string?>());

    private static bool TryReasonAndRequest(string? rawReason, Guid? rawRequestId, out string reason, out Guid requestId, out string? error)
    {
        try { reason = AdminReason.Required(rawReason); }
        catch (ArgumentException exception) { reason = string.Empty; requestId = Guid.Empty; error = exception.Message; return false; }
        if (rawRequestId is not Guid value || value == Guid.Empty) { requestId = Guid.Empty; error = "Eine eindeutige RequestId ist erforderlich."; return false; }
        requestId = value; error = null; return true;
    }

    private static bool TryIdentity(string raw, out CommunityIdentityId id)
    {
        id = default;
        if (!Guid.TryParse(raw, out var value) || value == Guid.Empty) return false;
        try { id = CommunityIdentityId.Create(value); return true; } catch (ArgumentException) { return false; }
    }

    private static IResult Invalid(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
}
