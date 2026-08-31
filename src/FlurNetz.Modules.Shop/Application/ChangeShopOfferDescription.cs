using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Ändert oder entfernt die Beschreibung eines vorhandenen Shop-Angebots.
/// </summary>
public sealed class ChangeShopOfferDescription
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Beschreibungs-Use-Case.
    /// </summary>
    public ChangeShopOfferDescription(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Beschreibungsänderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.ChangeDescription(description),
            cancellationToken);
    }
}
