using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt ein einzelnes Shop-Angebot aus dem internen Katalog.
/// </summary>
public sealed class GetShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Lookup-Use-Case.
    /// </summary>
    public GetShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert das Angebot oder <see langword="null"/>, wenn es unbekannt ist.
    /// </summary>
    public Task<ShopOffer?> ExecuteAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        return store.GetAsync(shopOfferId, cancellationToken);
    }
}
