using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt ein aktuell öffentlich sichtbares Shop-Angebot.
/// </summary>
public sealed class GetAvailableShopOffer
{
    private readonly IShopOfferStore store;
    private readonly IClock clock;

    /// <summary>
    /// Erstellt den Storefront-Lookup-Use-Case.
    /// </summary>
    public GetAvailableShopOffer(IShopOfferStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    /// <summary>
    /// Liefert das Angebot oder <see langword="null"/>, wenn es nicht öffentlich sichtbar ist.
    /// </summary>
    public async Task<ShopOffer?> ExecuteAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);
        var offer = await store.GetAsync(validShopOfferId, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        return offer is not null
            && offer.IsEnabled
            && !offer.IsArchived
            && offer.IsAvailableAt(now)
            ? offer
            : null;
    }
}
