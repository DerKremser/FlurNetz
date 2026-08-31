using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Definiert die eine atomare Infrastrukturgrenze für einen Shop-Kauf.
/// </summary>
/// <remarks>
/// Idempotenz, Offer-Snapshot, Kauflimit, Economy-Debit, Inventory-Grant, Purchase-Write
/// und Outbox werden hinter diesem gezielten Port in einer gemeinsamen PostgreSQL-Transaktion
/// koordiniert.
/// </remarks>
public interface IShopPurchaseExecutor
{
    Task<ShopPurchase> ExecuteAsync(
        ShopPurchaseRequestId requestId,
        ShopPurchaseId purchaseId,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        DateTimeOffset purchasedAtUtc,
        CancellationToken cancellationToken = default);
}
