using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Lädt die read-only Kaufhistorie einer einzelnen Community-Identität.
/// </summary>
public sealed class ListShopPurchasesForIdentity
{
    /// <summary>
    /// Standardseitengröße der Kaufhistorie.
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Kleinste zulässige Seitengröße.
    /// </summary>
    public const int MinimumPageSize = 1;

    /// <summary>
    /// Größte zulässige Seitengröße.
    /// </summary>
    public const int MaximumPageSize = 100;

    private readonly IShopPurchaseHistoryStore store;

    /// <summary>
    /// Erstellt den History-Use-Case.
    /// </summary>
    public ListShopPurchasesForIdentity(IShopPurchaseHistoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Lädt eine Seite newest-first mit stabiler Keyset-Paginierung.
    /// </summary>
    public async Task<ShopPurchaseHistoryPage> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        ShopPurchaseHistoryCursor? cursor = null,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        EnsureValidPageSize(pageSize);

        if (cursor is not null && cursor.CommunityIdentityId != validCommunityIdentityId)
        {
            throw new ArgumentException(
                "Der History-Cursor gehört zu einer anderen Community-Identität.",
                nameof(cursor));
        }

        var purchases = await store.ListForIdentityAsync(
                validCommunityIdentityId,
                cursor,
                pageSize + 1,
                cancellationToken)
            .ConfigureAwait(false);

        var hasMore = purchases.Count > pageSize;
        var items = hasMore
            ? purchases.Take(pageSize).ToArray()
            : purchases.ToArray();
        var nextCursor = hasMore
            ? CreateCursor(validCommunityIdentityId, items[^1])
            : null;

        return new ShopPurchaseHistoryPage(items, nextCursor);
    }

    private static void EnsureValidPageSize(int pageSize)
    {
        if (pageSize is < MinimumPageSize or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Die Seitengröße muss zwischen {MinimumPageSize} und {MaximumPageSize} liegen.");
        }
    }

    private static ShopPurchaseHistoryCursor CreateCursor(
        CommunityIdentityId communityIdentityId,
        ShopPurchase purchase) =>
        new(communityIdentityId, purchase.PurchasedAtUtc, purchase.Id);
}
