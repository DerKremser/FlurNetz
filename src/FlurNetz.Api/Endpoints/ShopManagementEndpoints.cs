using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        endpoints.MapGet(OffersRoute, ListShopOffersAsync);
        endpoints.MapGet($"{OffersRoute}/{{offerId}}", GetShopOfferAsync);
        endpoints.MapPost(OffersRoute, CreateShopOfferAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/display-name",
            RenameShopOfferAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/description",
            ChangeShopOfferDescriptionAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/price",
            ChangeShopOfferPriceAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/availability",
            ChangeShopOfferAvailabilityAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/purchase-limit",
            ChangeShopOfferPurchaseLimitAsync);
        endpoints.MapPut(
            $"{OffersRoute}/{{offerId}}/sort-order",
            ChangeShopOfferSortOrderAsync);
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/enable",
            EnableShopOfferAsync);
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/disable",
            DisableShopOfferAsync);
        endpoints.MapPost(
            $"{OffersRoute}/{{offerId}}/archive",
            ArchiveShopOfferAsync);

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
        CreateShopOfferRequest? request,
        CreateShopOffer useCase,
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
            var offer = await useCase.ExecuteAsync(
                    ItemDefinitionId.Create(request.ItemDefinitionId),
                    request.DisplayName!,
                    request.Description,
                    ShopPrice.Create(priceValue),
                    AvailabilityWindow.Create(
                        request.AvailableFromUtc,
                        request.AvailableUntilUtc),
                    request.PurchaseLimitPerIdentity,
                    cancellationToken,
                    request.SortOrder ?? 0)
                .ConfigureAwait(false);

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
        RenameShopOfferRequest? request,
        RenameShopOffer useCase,
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

        return await ExecuteMutationAsync(
                () => useCase.ExecuteAsync(
                    validOfferId,
                    request.DisplayName!,
                    cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferDescriptionAsync(
        string offerId,
        ChangeShopOfferDescriptionRequest? request,
        ChangeShopOfferDescription useCase,
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

        return await ExecuteMutationAsync(
                () => useCase.ExecuteAsync(
                    validOfferId,
                    request.Description,
                    cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferPriceAsync(
        string offerId,
        ChangeShopOfferPriceRequest? request,
        ChangeShopOfferPrice useCase,
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
            return await ExecuteMutationAsync(
                    () => useCase.ExecuteAsync(validOfferId, price, cancellationToken))
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ChangeShopOfferAvailabilityAsync(
        string offerId,
        ChangeShopOfferAvailabilityRequest? request,
        ChangeShopOfferAvailability useCase,
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
            return await ExecuteMutationAsync(
                    () => useCase.ExecuteAsync(validOfferId, availability, cancellationToken))
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ChangeShopOfferPurchaseLimitAsync(
        string offerId,
        ChangeShopOfferPurchaseLimitRequest? request,
        ChangeShopOfferPurchaseLimit useCase,
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

        return await ExecuteMutationAsync(
                () => useCase.ExecuteAsync(
                    validOfferId,
                    request.PurchaseLimitPerIdentity,
                    cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeShopOfferSortOrderAsync(
        string offerId,
        ChangeShopOfferSortOrderRequest? request,
        ChangeShopOfferSortOrder useCase,
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

        return await ExecuteMutationAsync(
                () => useCase.ExecuteAsync(validOfferId, sortOrder, cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<IResult> EnableShopOfferAsync(
        string offerId,
        EnableShopOffer useCase,
        CancellationToken cancellationToken) =>
        await ExecuteStatusMutationAsync(
                offerId,
                useCase.ExecuteAsync,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> DisableShopOfferAsync(
        string offerId,
        DisableShopOffer useCase,
        CancellationToken cancellationToken) =>
        await ExecuteStatusMutationAsync(
                offerId,
                useCase.ExecuteAsync,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> ArchiveShopOfferAsync(
        string offerId,
        ArchiveShopOffer useCase,
        CancellationToken cancellationToken) =>
        await ExecuteStatusMutationAsync(
                offerId,
                useCase.ExecuteAsync,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> ExecuteStatusMutationAsync(
        string rawOfferId,
        Func<ShopOfferId, CancellationToken, Task<bool>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawOfferId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        return await ExecuteMutationAsync(
                () => operation(validOfferId, cancellationToken))
            .ConfigureAwait(false);
    }

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
