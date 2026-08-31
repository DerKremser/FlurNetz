using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Ändert oder entfernt das Kauflimit eines vorhandenen Shop-Angebots.
/// </summary>
public sealed class ChangeShopOfferPurchaseLimit
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Kauflimit-Use-Case.
    /// </summary>
    public ChangeShopOfferPurchaseLimit(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Kauflimitänderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        int? purchaseLimitPerIdentity,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.ChangePurchaseLimit(purchaseLimitPerIdentity),
            cancellationToken);
    }
}
