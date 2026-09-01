using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Unveränderliches Ergebnis einer Seite der Shop-Kaufhistorie.
/// </summary>
public sealed class ShopPurchaseHistoryPage
{
    /// <summary>
    /// Erstellt eine History-Seite und kopiert deren Items in eine unveränderliche Liste.
    /// </summary>
    public ShopPurchaseHistoryPage(
        IReadOnlyList<ShopPurchase> items,
        ShopPurchaseHistoryCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        NextCursor = nextCursor;
    }

    /// <summary>
    /// Liefert die tatsächlich ausgegebenen Käufe.
    /// </summary>
    public IReadOnlyList<ShopPurchase> Items { get; }

    /// <summary>
    /// Liefert den Cursor für die nächste Seite oder <see langword="null"/> am Ende.
    /// </summary>
    public ShopPurchaseHistoryCursor? NextCursor { get; }
}
