using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Ändert die fachliche Reihenfolgeposition eines vorhandenen Shop-Angebots.
/// </summary>
public sealed class ChangeShopOfferSortOrder
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den SortOrder-Use-Case.
    /// </summary>
    public ChangeShopOfferSortOrder(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche SortOrder-Änderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.ChangeSortOrder(sortOrder),
            cancellationToken);
    }
}
