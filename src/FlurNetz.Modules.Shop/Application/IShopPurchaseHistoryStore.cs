using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Definiert die interne Read-Persistenzgrenze für abgeschlossene Shop-Käufe.
/// </summary>
/// <remarks>
/// Die Grenze enthält ausschließlich fachliche Shop-Typen. Technische Connection-,
/// Transaction-, Dapper- und Npgsql-Typen bleiben im Persistence-Adapter.
/// </remarks>
public interface IShopPurchaseHistoryStore
{
    /// <summary>
    /// Lädt einen Kauf über seine stabile ID oder liefert bei unbekannter ID
    /// <see langword="null"/>.
    /// </summary>
    Task<ShopPurchase?> GetAsync(
        ShopPurchaseId shopPurchaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt höchstens <paramref name="take"/> Käufe einer Identität in newest-first-
    /// Reihenfolge, optional hinter dem übergebenen Seek-Cursor.
    /// </summary>
    Task<IReadOnlyList<ShopPurchase>> ListForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        ShopPurchaseHistoryCursor? cursor,
        int take,
        CancellationToken cancellationToken = default);
}
