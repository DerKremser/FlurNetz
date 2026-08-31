using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Aktiviert ein vorhandenes Shop-Angebot.
/// </summary>
public sealed class EnableShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Aktivierungs-Use-Case.
    /// </summary>
    public EnableShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Aktivierung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.Enable(),
            cancellationToken);
    }
}
