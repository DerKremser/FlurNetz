using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt den vollständigen internen Shop-Angebotskatalog.
/// </summary>
public sealed class ListShopOffers
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den List-Use-Case.
    /// </summary>
    public ListShopOffers(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert alle Angebote in der vom Store definierten deterministischen Reihenfolge.
    /// </summary>
    public Task<IReadOnlyList<ShopOffer>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(cancellationToken);
    }
}
