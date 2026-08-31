using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Signalisiert, dass ein vorhandenes Angebot zum Kaufzeitpunkt nicht kaufbar ist.
/// </summary>
public sealed class ShopOfferUnavailableForPurchaseException : InvalidOperationException
{
    public ShopOfferUnavailableForPurchaseException(ShopOfferId shopOfferId)
        : base($"Das Shop-Angebot '{ShopOfferId.Create(shopOfferId.Value).Value}' ist derzeit nicht kaufbar.")
    {
        ShopOfferId = ShopOfferId.Create(shopOfferId.Value);
    }

    public ShopOfferId ShopOfferId { get; }
}
