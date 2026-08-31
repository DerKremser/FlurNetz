using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Ändert das Verfügbarkeitsfenster eines vorhandenen Shop-Angebots.
/// </summary>
public sealed class ChangeShopOfferAvailability
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Availability-Use-Case.
    /// </summary>
    public ChangeShopOfferAvailability(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Availability-Änderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        AvailabilityWindow availabilityWindow,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.ChangeAvailability(availabilityWindow),
            cancellationToken);
    }
}
