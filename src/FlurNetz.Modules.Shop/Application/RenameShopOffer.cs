using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Benennt ein vorhandenes Shop-Angebot über seine Domain-Mutation um.
/// </summary>
public sealed class RenameShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Rename-Use-Case.
    /// </summary>
    public RenameShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Umbenennung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            shopOfferId,
            offer => offer.Rename(displayName),
            cancellationToken);
    }
}
