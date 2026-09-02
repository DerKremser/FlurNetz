using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Archiviert ein vorhandenes Shop-Angebot endgültig.
/// </summary>
public sealed class ArchiveShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Archivierungs-Use-Case.
    /// </summary>
    public ArchiveShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Archivierung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.Archive(),
            cancellationToken);
    }
}
