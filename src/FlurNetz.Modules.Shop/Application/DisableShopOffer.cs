using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Deaktiviert ein vorhandenes Shop-Angebot.
/// </summary>
public sealed class DisableShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Deaktivierungs-Use-Case.
    /// </summary>
    public DisableShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Deaktivierung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.Disable(),
            cancellationToken);
    }
}
