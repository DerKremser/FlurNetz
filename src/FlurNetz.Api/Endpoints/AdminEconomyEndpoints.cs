using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

public static class AdminEconomyEndpoints
{
    private const string Route = "/api/admin/economy";

    public static IEndpointRouteBuilder MapAdminEconomyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/economy", ReadAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.EconomyRead));
        endpoints.MapPost($"{Route}/{{communityIdentityId}}/credit", CreditAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.EconomyAdjust))
            .RequireAntiforgery();
        endpoints.MapPost($"{Route}/{{communityIdentityId}}/debit", DebitAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.EconomyAdjust))
            .RequireAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ReadAsync(string communityIdentityId, ICommunityEconomyStore store, CancellationToken token)
    {
        if (!TryId(communityIdentityId, out var id)) return Invalid("Die Identity-ID ist ungültig.");
        var economy = await store.GetByCommunityIdentityIdAsync(id, token).ConfigureAwait(false);
        return economy is null ? Results.NotFound() : Results.Ok(new AdminEconomyResponse(economy.Balance.Value));
    }

    private static Task<IResult> CreditAsync(string communityIdentityId, [FromBody] AdminEconomyAdjustmentRequest? request, IAdminExecutionContextAccessor contextAccessor, ICommunityIdentityExistence identityExistence, IEconomyBalanceCredit credit, ICommunityEconomyStore economyStore, AdminMutationCoordinator coordinator, IAdminAuditStore auditStore, CancellationToken token) =>
        AdjustAsync(communityIdentityId, request, true, contextAccessor, identityExistence, credit, null, economyStore, coordinator, token);

    private static Task<IResult> DebitAsync(string communityIdentityId, [FromBody] AdminEconomyAdjustmentRequest? request, IAdminExecutionContextAccessor contextAccessor, ICommunityIdentityExistence identityExistence, IEconomyBalanceDebit debit, ICommunityEconomyStore economyStore, AdminMutationCoordinator coordinator, CancellationToken token) =>
        AdjustAsync(communityIdentityId, request, false, contextAccessor, identityExistence, null, debit, economyStore, coordinator, token);

    private static async Task<IResult> AdjustAsync(
        string rawId,
        AdminEconomyAdjustmentRequest? request,
        bool credit,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        IEconomyBalanceCredit? creditCapability,
        IEconomyBalanceDebit? debitCapability,
        ICommunityEconomyStore economyStore,
        AdminMutationCoordinator coordinator,
        CancellationToken token)
    {
        if (!TryId(rawId, out var identityId)) return Invalid("Die Identity-ID ist ungültig.");
        if (request?.Amount is not long amount || amount <= 0) return Invalid("Der Betrag muss positiv sein.");
        if (request.RequestId is not Guid requestId || requestId == Guid.Empty) return Invalid("Eine eindeutige RequestId ist erforderlich.");
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        string reason;
        try { reason = AdminReason.Required(request.Reason); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }

        var action = credit ? AdminAuditActions.BalanceCredited : AdminAuditActions.BalanceDebited;
        var operationType = credit ? "Economy.BalanceCredit" : "Economy.BalanceDebit";
        var fingerprint = AdminRequestFingerprint.Compute(("identity", identityId.Value), ("amount", amount), ("reason", reason), ("direction", credit ? "credit" : "debit"));
        try
        {
            var mutation = await coordinator.ExecuteAsync(
                new AdminMutationCommand(requestId, context.ActorCommunityIdentityId, operationType, "CommunityIdentity", identityId.Value.ToString("D"), fingerprint, context.CorrelationId, DateTimeOffset.UtcNow),
                async (connection, transaction, cancellationToken) =>
                {
                    if (!await identityExistence.ExistsAsync(identityId, connection, transaction, cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException();
                    if (credit) await creditCapability!.CreditAsync(identityId, amount, connection, transaction, cancellationToken).ConfigureAwait(false);
                    else await debitCapability!.DebitAsync(identityId, amount, connection, transaction, cancellationToken).ConfigureAwait(false);
                },
                () => new AdminAuditEntry(Guid.NewGuid(), context.ActorCommunityIdentityId, context.ActorCommunityIdentityId.Value.ToString("D"), action, "CommunityIdentity", identityId.Value.ToString("D"), null, AdminRiskLevel.High, reason, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow, context.CorrelationId, requestId, null, new Dictionary<string, string?> { ["Direction"] = credit ? "Credit" : "Debit", ["Amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture) }, new Dictionary<string, string?>()),
                token).ConfigureAwait(false);
            var balance = await economyStore.GetByCommunityIdentityIdAsync(identityId, token).ConfigureAwait(false);
            return balance is null ? Results.NotFound() : Results.Ok(new AdminEconomyAdjustmentResponse(identityId.Value, balance.Balance.Value, mutation.AlreadyCompleted));
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static bool TryId(string raw, out CommunityIdentityId id)
    {
        id = default;
        return Guid.TryParse(raw, out var value) && value != Guid.Empty && TryCreate(value, out id);
    }

    private static bool TryCreate(Guid value, out CommunityIdentityId id)
    {
        try { id = CommunityIdentityId.Create(value); return true; }
        catch (ArgumentException) { id = default; return false; }
    }

    private static IResult Invalid(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
}
