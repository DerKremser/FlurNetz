using System.Globalization;
using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlurNetz.Api.Endpoints;

/// <summary>
/// Ordnet die read-only HTTP-Grenze des Shop-Vertical-Slices zu.
/// </summary>
public static class ShopEndpoints
{
    /// <summary>
    /// Registriert ausschließlich die öffentlichen Shop-Leseendpunkte.
    /// </summary>
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/shop/offers", ListAvailableShopOffersAsync);
        endpoints.MapGet("/api/shop/offers/{offerId}", GetAvailableShopOfferAsync);
        endpoints.MapGet("/api/shop/purchases/{purchaseId}", GetShopPurchaseAsync);
        endpoints.MapGet(
            "/api/shop/identities/{communityIdentityId}/purchases",
            ListShopPurchasesForIdentityAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAvailableShopOffersAsync(
        ListAvailableShopOffers useCase,
        CancellationToken cancellationToken)
    {
        var offers = await useCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(new ShopOfferListResponse(offers.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> GetAvailableShopOfferAsync(
        string offerId,
        GetAvailableShopOffer useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(offerId, ShopOfferId.Create, out var validOfferId))
        {
            return InvalidRequest("Die Route-ID des Shop-Angebots ist ungültig.");
        }

        var offer = await useCase.ExecuteAsync(validOfferId, cancellationToken).ConfigureAwait(false);
        return offer is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(offer));
    }

    private static async Task<IResult> GetShopPurchaseAsync(
        string purchaseId,
        GetShopPurchase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(purchaseId, ShopPurchaseId.Create, out var validPurchaseId))
        {
            return InvalidRequest("Die Route-ID des Shop-Purchases ist ungültig.");
        }

        var purchase = await useCase.ExecuteAsync(validPurchaseId, cancellationToken).ConfigureAwait(false);
        return purchase is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(purchase));
    }

    private static async Task<IResult> ListShopPurchasesForIdentityAsync(
        string communityIdentityId,
        string? pageSize,
        string? cursor,
        ListShopPurchasesForIdentity useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var validIdentityId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität ist ungültig.");
        }

        if (!TryParsePageSize(pageSize, out var validPageSize))
        {
            return InvalidRequest("Die Seitengröße muss zwischen 1 und 100 liegen.");
        }

        ShopPurchaseHistoryCursor? historyCursor = null;
        if (cursor is not null
            && !ShopPurchaseHistoryCursorCodec.TryDecode(
                cursor,
                validIdentityId,
                out historyCursor,
                out _))
        {
            return InvalidRequest("Der History-Cursor ist ungültig.");
        }

        var page = await useCase.ExecuteAsync(
                validIdentityId,
                historyCursor,
                validPageSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new ShopPurchaseHistoryResponse(
            page.Items.Select(ToResponse).ToArray(),
            page.NextCursor is null ? null : ShopPurchaseHistoryCursorCodec.Encode(page.NextCursor)));
    }

    private static ShopOfferResponse ToResponse(ShopOffer offer) =>
        new(
            offer.Id.Value,
            offer.ItemDefinitionIdValue,
            offer.DisplayName,
            offer.Description,
            offer.Price.Value,
            offer.Availability.AvailableFrom,
            offer.Availability.AvailableUntil,
            offer.PurchaseLimitPerIdentity);

    private static ShopPurchaseResponse ToResponse(ShopPurchase purchase) =>
        new(
            purchase.Id.Value,
            purchase.ShopOfferId.Value,
            purchase.CommunityIdentityId.Value,
            purchase.ItemDefinitionIdValue,
            purchase.PricePaid.Value,
            purchase.PurchasedAtUtc);

    private static bool TryParsePageSize(string? rawPageSize, out int pageSize)
    {
        if (rawPageSize is null)
        {
            pageSize = ListShopPurchasesForIdentity.DefaultPageSize;
            return true;
        }

        return int.TryParse(
                rawPageSize,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out pageSize)
            && pageSize is >= ListShopPurchasesForIdentity.MinimumPageSize
                and <= ListShopPurchasesForIdentity.MaximumPageSize;
    }

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
