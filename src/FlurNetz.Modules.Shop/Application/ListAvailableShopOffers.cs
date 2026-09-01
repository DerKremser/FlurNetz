using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt die aktuell öffentlich sichtbaren Shop-Angebote.
/// </summary>
public sealed class ListAvailableShopOffers
{
    private readonly IShopOfferStore store;
    private readonly IClock clock;

    /// <summary>
    /// Erstellt den Storefront-List-Use-Case.
    /// </summary>
    public ListAvailableShopOffers(IShopOfferStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    /// <summary>
    /// Liefert den vollständigen Katalog nach einem gemeinsamen Zeitpunkt gefiltert.
    /// </summary>
    public async Task<IReadOnlyList<ShopOffer>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var offers = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        return Array.AsReadOnly(
            offers
                .Where(offer => offer.IsEnabled && offer.IsAvailableAt(now))
                .ToArray());
    }
}
