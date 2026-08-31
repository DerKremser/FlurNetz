using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Ändert den Preis eines vorhandenen Shop-Angebots.
/// </summary>
public sealed class ChangeShopOfferPrice
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Preis-Use-Case.
    /// </summary>
    public ChangeShopOfferPrice(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Preisänderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        ShopPrice price,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.ChangePrice(price),
            cancellationToken);
    }
}
