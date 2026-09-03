using System.Globalization;
using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Modules.Progression.Application;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

public static class AdminProgressionInventoryEndpoints
{
    public static IEndpointRouteBuilder MapAdminProgressionInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/progression", ReadProgressionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ProgressionRead));
        endpoints.MapPost("/api/admin/progression/{communityIdentityId}/grant-experience", GrantExperienceAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ProgressionGrantExperience))
            .RequireAntiforgery();

        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/inventory", ListInventoryAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.InventoryRead));
        endpoints.MapGet("/api/admin/identities/{communityIdentityId}/inventory/{itemDefinitionId}", GetInventoryAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.InventoryRead));
        endpoints.MapPost("/api/admin/inventory/{communityIdentityId}/add", AddInventoryAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.InventoryAdjust))
            .RequireAntiforgery();
        endpoints.MapPost("/api/admin/inventory/{communityIdentityId}/remove", RemoveInventoryAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.InventoryAdjust))
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ReadProgressionAsync(
        string communityIdentityId,
        ICommunityProgressionStore store,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Die Identity-ID ist ungültig.");
        var progression = await store.GetByCommunityIdentityIdAsync(identity, token).ConfigureAwait(false);
        return progression is null
            ? Results.NotFound()
            : Results.Ok(new AdminProgressionResponseV1(identity.Value, progression.ExperiencePoints.Value));
    }

    private static async Task<IResult> GrantExperienceAsync(
        string communityIdentityId,
        [FromBody] AdminProgressionGrantRequest? request,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        ICommunityProgressionStore progressionStore,
        AdminMutationCoordinator coordinator,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Die Identity-ID ist ungültig.");
        if (request?.ExperiencePoints is not long amount || amount <= 0)
        {
            return Invalid("Die XP-Menge muss positiv sein.");
        }

        if (!TryReasonAndRequest(request.Reason, request.RequestId, out var reason, out var requestId, out var error))
        {
            return Invalid(error!);
        }

        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();

        return await ExecuteAtomicAsync(
                contextAccessor,
                coordinator,
                new AdminMutationCommand(
                    requestId,
                    context.ActorCommunityIdentityId,
                    "Progression.ExperienceGrant",
                    "CommunityIdentity",
                    identity.Value.ToString("D"),
                    AdminRequestFingerprint.Compute(("identity", identity.Value), ("amount", amount), ("reason", reason)),
                    context.CorrelationId,
                    DateTimeOffset.UtcNow),
                async (connection, transaction, cancellationToken) =>
                {
                    if (!await identityExistence.ExistsAsync(identity, connection, transaction, cancellationToken).ConfigureAwait(false))
                    {
                        throw new KeyNotFoundException();
                    }

                    await progressionStore.GrantExperienceAsync(
                            identity,
                            amount,
                            connection,
                            transaction,
                            cancellationToken)
                        .ConfigureAwait(false);
                },
                AdminAuditActions.ExperienceGranted,
                "CommunityIdentity",
                identity.Value.ToString("D"),
                AdminRiskLevel.High,
                reason,
                requestId,
                new Dictionary<string, string?>
                {
                    ["ExperienceGranted"] = amount.ToString(CultureInfo.InvariantCulture)
                },
                token)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ListInventoryAsync(
        string communityIdentityId,
        ICommunityInventoryStore store,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Die Identity-ID ist ungültig.");
        var entries = await store.ListAsync(identity, token).ConfigureAwait(false);
        return Results.Ok(entries.Select(entry => new AdminInventoryEntryResponse(
            entry.ItemDefinitionId.Value,
            entry.Quantity.Value)).ToArray());
    }

    private static async Task<IResult> GetInventoryAsync(
        string communityIdentityId,
        string itemDefinitionId,
        ICommunityInventoryStore store,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity) || !TryItem(itemDefinitionId, out var item))
        {
            return Invalid("Die Identity- oder ItemDefinition-ID ist ungültig.");
        }

        var entry = await store.GetAsync(identity, item, token).ConfigureAwait(false);
        return entry is null
            ? Results.NotFound()
            : Results.Ok(new AdminInventoryEntryResponse(item.Value, entry.Quantity.Value));
    }

    private static Task<IResult> AddInventoryAsync(
        string communityIdentityId,
        [FromBody] AdminInventoryAdjustmentRequest? request,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        ICommunityInventoryStore inventoryStore,
        AdminMutationCoordinator coordinator,
        CancellationToken token) =>
        AdjustInventoryAsync(communityIdentityId, request, true, contextAccessor, identityExistence, inventoryStore, coordinator, token);

    private static Task<IResult> RemoveInventoryAsync(
        string communityIdentityId,
        [FromBody] AdminInventoryAdjustmentRequest? request,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        ICommunityInventoryStore inventoryStore,
        AdminMutationCoordinator coordinator,
        CancellationToken token) =>
        AdjustInventoryAsync(communityIdentityId, request, false, contextAccessor, identityExistence, inventoryStore, coordinator, token);

    private static async Task<IResult> AdjustInventoryAsync(
        string communityIdentityId,
        AdminInventoryAdjustmentRequest? request,
        bool add,
        IAdminExecutionContextAccessor contextAccessor,
        ICommunityIdentityExistence identityExistence,
        ICommunityInventoryStore inventoryStore,
        AdminMutationCoordinator coordinator,
        CancellationToken token)
    {
        if (!TryIdentity(communityIdentityId, out var identity)) return Invalid("Die Identity-ID ist ungültig.");
        if (request?.ItemDefinitionId is not Guid rawItem || rawItem == Guid.Empty || !TryItem(rawItem, out var item))
        {
            return Invalid("Die ItemDefinition-ID ist erforderlich.");
        }

        if (request.Quantity is not long amount || amount <= 0)
        {
            return Invalid("Die Menge muss positiv sein.");
        }

        if (!TryReasonAndRequest(request.Reason, request.RequestId, out var reason, out var requestId, out var error))
        {
            return Invalid(error!);
        }

        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        var operationType = add ? "Inventory.QuantityAdd" : "Inventory.QuantityRemove";
        var action = add ? AdminAuditActions.QuantityAdded : AdminAuditActions.QuantityRemoved;

        try
        {
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestId,
                        context.ActorCommunityIdentityId,
                        operationType,
                        "CommunityIdentity",
                        identity.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("identity", identity.Value), ("item", item.Value), ("amount", amount), ("direction", add ? "add" : "remove"), ("reason", reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, cancellationToken) =>
                    {
                        if (!await identityExistence.ExistsAsync(identity, connection, transaction, cancellationToken).ConfigureAwait(false))
                        {
                            throw new KeyNotFoundException();
                        }

                        if (add)
                        {
                            await inventoryStore.AddAsync(identity, item, amount, connection, transaction, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await inventoryStore.RemoveAsync(identity, item, amount, connection, transaction, cancellationToken).ConfigureAwait(false);
                        }
                    },
                    () => new AdminAuditEntry(
                        Guid.NewGuid(),
                        context.ActorCommunityIdentityId,
                        context.ActorLoginName,
                        action,
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
                            ["ItemDefinitionId"] = item.Value.ToString("D"),
                            ["QuantityDelta"] = (add ? amount : -amount).ToString(CultureInfo.InvariantCulture)
                        },
                        new Dictionary<string, string?>()),
                    token)
                .ConfigureAwait(false);

            var current = await inventoryStore.GetAsync(identity, item, token).ConfigureAwait(false);
            return Results.Ok(new
            {
                communityIdentityId = identity.Value,
                itemDefinitionId = item.Value,
                quantity = current?.Quantity.Value ?? 0,
                alreadyCompleted = mutation.AlreadyCompleted
            });
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InsufficientInventoryQuantityException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static async Task<IResult> ExecuteAtomicAsync(
        IAdminExecutionContextAccessor contextAccessor,
        AdminMutationCoordinator coordinator,
        AdminMutationCommand command,
        Func<System.Data.Common.DbConnection, System.Data.Common.DbTransaction, CancellationToken, Task> ownerMutation,
        string action,
        string targetType,
        string targetId,
        AdminRiskLevel risk,
        string reason,
        Guid requestId,
        IReadOnlyDictionary<string, string?> summary,
        CancellationToken token)
    {
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            var mutation = await coordinator.ExecuteAsync(
                    command,
                    ownerMutation,
                    () => new AdminAuditEntry(
                        Guid.NewGuid(),
                        context.ActorCommunityIdentityId,
                        context.ActorLoginName,
                        action,
                        targetType,
                        targetId,
                        null,
                        risk,
                        reason,
                        AdminAuditOutcome.Succeeded,
                        DateTimeOffset.UtcNow,
                        context.CorrelationId,
                        requestId,
                        null,
                        summary,
                        new Dictionary<string, string?>()),
                    token)
                .ConfigureAwait(false);
            return Results.Ok(new AdminAlreadyCompletedResponse(mutation.AlreadyCompleted));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (ArgumentException exception) { return Invalid(exception.Message); }
    }

    private static bool TryReasonAndRequest(
        string? rawReason,
        Guid? rawRequestId,
        out string reason,
        out Guid requestId,
        out string? error)
    {
        try
        {
            reason = AdminReason.Required(rawReason);
        }
        catch (ArgumentException exception)
        {
            reason = string.Empty;
            requestId = Guid.Empty;
            error = exception.Message;
            return false;
        }

        if (rawRequestId is not Guid validRequestId || validRequestId == Guid.Empty)
        {
            requestId = Guid.Empty;
            error = "Eine eindeutige RequestId ist erforderlich.";
            return false;
        }

        requestId = validRequestId;
        error = null;
        return true;
    }

    private static bool TryIdentity(string raw, out CommunityIdentityId id) => TryGuid(raw, CommunityIdentityId.Create, out id);

    private static bool TryItem(string raw, out ItemDefinitionId id) => TryGuid(raw, ItemDefinitionId.Create, out id);

    private static bool TryItem(Guid raw, out ItemDefinitionId id)
    {
        try { id = ItemDefinitionId.Create(raw); return true; }
        catch (ArgumentException) { id = default; return false; }
    }

    private static bool TryGuid<T>(string raw, Func<Guid, T> create, out T value)
    {
        value = default!;
        if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty) return false;
        try { value = create(parsed); return true; }
        catch (ArgumentException) { return false; }
    }

    private static IResult Invalid(string detail) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Ungültige Anfrage.",
        detail: detail);
}
