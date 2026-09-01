using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Unveränderlicher Seek-Cursor für die Kaufhistorie genau einer Community-Identität.
/// </summary>
public sealed record ShopPurchaseHistoryCursor
{
    /// <summary>
    /// Erstellt einen validierten History-Cursor.
    /// </summary>
    public ShopPurchaseHistoryCursor(
        CommunityIdentityId communityIdentityId,
        DateTimeOffset purchasedAtUtc,
        ShopPurchaseId shopPurchaseId)
    {
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        PurchasedAtUtc = EnsureValidPurchasedAtUtc(purchasedAtUtc);
        ShopPurchaseId = ShopPurchaseId.Create(shopPurchaseId.Value);
    }

    /// <summary>
    /// Liefert die Identität, an die dieser Cursor gebunden ist.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert den letzten ausgegebenen Kaufzeitpunkt.
    /// </summary>
    public DateTimeOffset PurchasedAtUtc { get; }

    /// <summary>
    /// Liefert die letzte ausgegebene Purchase-ID.
    /// </summary>
    public ShopPurchaseId ShopPurchaseId { get; }

    /// <summary>
    /// Erstellt einen validierten History-Cursor.
    /// </summary>
    public static ShopPurchaseHistoryCursor Create(
        CommunityIdentityId communityIdentityId,
        DateTimeOffset purchasedAtUtc,
        ShopPurchaseId shopPurchaseId) =>
        new(communityIdentityId, purchasedAtUtc, shopPurchaseId);

    private static DateTimeOffset EnsureValidPurchasedAtUtc(DateTimeOffset purchasedAtUtc)
    {
        if (purchasedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Der Cursor-Kaufzeitpunkt muss in UTC vorliegen.",
                nameof(purchasedAtUtc));
        }

        if (purchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Cursor-Kaufzeitpunkt muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                nameof(purchasedAtUtc));
        }

        return purchasedAtUtc;
    }
}
