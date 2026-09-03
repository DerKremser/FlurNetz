using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

/// <summary>
/// Ordnet die getrennte HTTP-Management-Grenze des internen Shop-Katalogs zu.
/// </summary>
public static class ShopManagementEndpoints
{
    private const string OffersRoute = "/api/admin/shop/offers";

    /// <summary>
    /// Registriert die internen Katalog-Lese- und Mutationsendpunkte.
    /// </summary>
    public static IEndpointRouteBuilder MapShopManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(OffersRoute, ListShopOffersAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopRead));
        endpoints.MapGet($"{OffersRoute}/{{offerId}}", GetShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopRead));
        endpoints.MapPost(OffersRoute, CreateShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/display-name",
            RenameShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/description",
            ChangeShopOfferDescriptionAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/price",
            ChangeShopOfferPriceAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/availability",
            ChangeShopOfferAvailabilityAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/purchase-limit",
            ChangeShopOfferPurchaseLimitAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/sort-order",
            ChangeShopOfferSortOrderAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/enable",
            EnableShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/disable",
            DisableShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/archive",
            ArchiveShopOfferAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.ShopManage))
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListShopOffersAsync(
        ListShopOffers useCase,
        CancellationToken cancellationToken)
    {
        var offers = await useCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(new ShopOfferManagementListResponse(
            offers.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> GetShopOfferAsync(
        string offerId,
        GetShopOffer useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        var offer = await useCase.ExecuteAsync(validOfferId, cancellationToken).ConfigureAwait(false);
        return offer is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Shop-Angebot nicht gefunden.",
                detail: $"Das Shop-Angebot '{validOfferId.Value}' wurde nicht gefunden.")
            : Results.Ok(ToResponse(offer));
    }

    private static async Task<IResult> CreateShopOfferAsync(
        [FromBody] CreateShopOfferRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        if (request.Price is not long priceValue)
        {
            return InvalidRequest("Der Preis ist erforderlich.");
        }

        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var offer = ShopOffer.Create(
                ShopOfferId.New(),
                ItemDefinitionId.Create(request.ItemDefinitionId),
                request.DisplayName!,
                request.Description,
                ShopPrice.Create(priceValue),
                AvailabilityWindow.Create(request.AvailableFromUtc, request.AvailableUntilUtc),
                request.PurchaseLimitPerIdentity,
                request.SortOrder ?? 0);
            await coordinator.ExecuteAuditedAsync(
                (connection, transaction, token) => store.AddAsync(offer, connection, transaction, token),
                () => NormalAudit(context, AdminAuditActions.OfferUpdated, offer.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }),
                cancellationToken).ConfigureAwait(false);

            return Results.Created(
                $"{OffersRoute}/{offer.Id.Value}",
                ToResponse(offer));
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> RenameShopOfferAsync(
        string offerId,
        [FromBody] RenameShopOfferRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.Rename(request.DisplayName!), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferDescriptionAsync(
        string offerId,
        [FromBody] ChangeShopOfferDescriptionRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.ChangeDescription(request.Description), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferPriceAsync(
        string offerId,
        [FromBody] ChangeShopOfferPriceRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        if (request.Price is not long priceValue)
        {
            return InvalidRequest("Der Preis ist erforderlich.");
        }

        try
        {
            var price = ShopPrice.Create(priceValue);
            return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.ChangePrice(price), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ChangeShopOfferAvailabilityAsync(
        string offerId,
        [FromBody] ChangeShopOfferAvailabilityRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        try
        {
            var availability = AvailabilityWindow.Create(
                request.AvailableFromUtc,
                request.AvailableUntilUtc);
            return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.ChangeAvailability(availability), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ChangeShopOfferPurchaseLimitAsync(
        string offerId,
        [FromBody] ChangeShopOfferPurchaseLimitRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.ChangePurchaseLimit(request.PurchaseLimitPerIdentity), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferSortOrderAsync(
        string offerId,
        [FromBody] ChangeShopOfferSortOrderRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        if (request.SortOrder is not int sortOrder)
        {
            return InvalidRequest("Die Sortierreihenfolge ist erforderlich.");
        }

        return await ExecuteAuditedMutationAsync(validOfferId, offer => offer.ChangeSortOrder(sortOrder), AdminAuditActions.OfferUpdated, store, coordinator, contextAccessor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> EnableShopOfferAsync(
        string offerId,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken) =>
        await ExecuteAuditedStatusMutationAsync(
                offerId,
                offer => offer.Enable(),
                AdminAuditActions.OfferEnabled,
                store,
                coordinator,
                contextAccessor,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> DisableShopOfferAsync(
        string offerId,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken) =>
        await ExecuteAuditedStatusMutationAsync(
                offerId,
                offer => offer.Disable(),
                AdminAuditActions.OfferDisabled,
                store,
                coordinator,
                contextAccessor,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> ArchiveShopOfferAsync(
        string offerId,
        [FromBody] AdminActionRequest? request,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        if (!TryHighRiskRequest(request, out var requestData, out var requestError))
        {
            return InvalidRequest(requestError!);
        }

        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();

        try
        {
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.OfferArchived,
                        "ShopOffer",
                        validOfferId.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(
                            ("offer", validOfferId.Value),
                            ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    (connection, transaction, token) => store.ExecuteAsync(
                        validOfferId,
                        offer => offer.Archive(),
                        connection,
                        transaction,
                        token),
                    () => CreateAudit(
                        context,
                        AdminAuditActions.OfferArchived,
                        validOfferId.Value.ToString("D"),
                        requestData.Reason,
                        requestData.RequestId,
                        new Dictionary<string, string?> { ["Archived"] = "true" }),
                    cancellationToken)
                .ConfigureAwait(false);

            return mutation.AlreadyCompleted
                ? Results.Ok(new AdminAlreadyCompletedResponse(true))
                : Results.NoContent();
        }
        catch (AdminOperationConflictException exception)
        {
            return Results.Conflict(new AdminErrorResponse(exception.Message));
        }
        catch (ShopOfferNotFoundException exception)
        {
            return Results.NotFound(new AdminErrorResponse(exception.Message));
        }
        catch (ShopOfferArchivedException exception)
        {
            return Results.Conflict(new AdminErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ExecuteAuditedStatusMutationAsync(
        string rawOfferId,
        Func<ShopOffer, bool> mutation,
        string action,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawOfferId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        return await ExecuteAuditedMutationAsync(validOfferId, mutation, action, store, coordinator, contextAccessor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ExecuteAuditedMutationAsync(
        ShopOfferId offerId,
        Func<ShopOffer, bool> mutation,
        string action,
        IShopOfferStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            await coordinator.ExecuteAuditedAsync(
                (connection, transaction, token) => store.ExecuteAsync(offerId, mutation, connection, transaction, token),
                () => NormalAudit(context, action, offerId.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (ShopOfferNotFoundException exception) { return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Shop-Angebot nicht gefunden.", detail: exception.Message); }
        catch (ShopOfferArchivedException exception) { return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Shop-Angebot archiviert.", detail: exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static bool TryHighRiskRequest(AdminActionRequest? request, out (Guid RequestId, string Reason) value, out string? error)
    {
        value = default;
        try
        {
            if (request?.RequestId is not Guid requestId || requestId == Guid.Empty)
            {
                throw new ArgumentException("Eine eindeutige RequestId ist erforderlich.");
            }

            value = (requestId, AdminReason.Required(request.Reason));
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
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
            "ShopOffer",
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

    private static AdminAuditEntry NormalAudit(
        AdminExecutionContext context,
        string action,
        string targetId,
        IReadOnlyDictionary<string, string?> changeSummary) =>
        new(
            Guid.NewGuid(),
            context.ActorCommunityIdentityId,
            context.ActorCommunityIdentityId.Value.ToString("D"),
            action,
            "ShopOffer",
            targetId,
            null,
            AdminRiskLevel.Medium,
            null,
            AdminAuditOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            context.CorrelationId,
            null,
            null,
            changeSummary,
            new Dictionary<string, string?>());

    private static async Task<IResult> ExecuteMutationAsync(Func<Task<bool>> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (ShopOfferNotFoundException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Shop-Angebot nicht gefunden.",
                detail: exception.Message);
        }
        catch (ShopOfferArchivedException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Shop-Angebot archiviert.",
                detail: exception.Message);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static ShopOfferManagementResponse ToResponse(ShopOffer offer) =>
        new(
            offer.Id.Value,
            offer.ItemDefinitionIdValue,
            offer.DisplayName,
            offer.Description,
            offer.Price.Value,
            offer.IsEnabled,
            offer.IsArchived,
            offer.Availability.AvailableFrom,
            offer.Availability.AvailableUntil,
            offer.PurchaseLimitPerIdentity,
            offer.SortOrder);

    private static bool TryCreateId<TId>(
        string rawId,
        Func<Guid, TId> create,
        out TId id)
    {
        id = default!;
        return Guid.TryParse(rawId, out var value)
            && value != Guid.Empty
            && TryCreate(value, create, out id);
    }

    private static bool TryCreate<TId>(Guid value, Func<Guid, TId> create, out TId id)
    {
        try
        {
            id = create(value);
            return true;
        }
        catch (ArgumentException)
        {
            id = default!;
            return false;
        }
    }

    private static IResult InvalidRequest(string detail) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Ungültige Anfrage.",
        detail: detail);
}
