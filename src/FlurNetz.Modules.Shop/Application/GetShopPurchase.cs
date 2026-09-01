using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt einen einzelnen abgeschlossenen Shop-Kauf aus der internen Historie.
/// </summary>
public sealed class GetShopPurchase
{
    private readonly IShopPurchaseHistoryStore store;

    /// <summary>
    /// Erstellt den Purchase-Lookup-Use-Case.
    /// </summary>
    public GetShopPurchase(IShopPurchaseHistoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert den Kauf oder <see langword="null"/>, wenn seine ID unbekannt ist.
    /// </summary>
    public Task<ShopPurchase?> ExecuteAsync(
        ShopPurchaseId shopPurchaseId,
        CancellationToken cancellationToken = default)
    {
        var validShopPurchaseId = ShopPurchaseId.Create(shopPurchaseId.Value);
        return store.GetAsync(validShopPurchaseId, cancellationToken);
    }
}
