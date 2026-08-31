using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Startet den atomaren Kauf eines einzelnen Inventory-Items aus einem Shop-Angebot.
/// </summary>
public sealed class PurchaseShopOffer
{
    private readonly IShopPurchaseExecutor executor;
    private readonly IClock clock;

    public PurchaseShopOffer(IShopPurchaseExecutor executor, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(clock);
        this.executor = executor;
        this.clock = clock;
    }

    /// <summary>
    /// Führt einen idempotenten Kaufrequest aus. Ein erfolgreicher neuer Kauf gewährt exakt ein Item.
    /// </summary>
    public Task<ShopPurchase> ExecuteAsync(
        ShopPurchaseRequestId requestId,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validRequestId = ShopPurchaseRequestId.Create(requestId.Value);
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var purchasedAtUtc = CanonicalizeToPostgreSqlMicroseconds(clock.UtcNow);

        return executor.ExecuteAsync(
            validRequestId,
            ShopPurchaseId.New(),
            validShopOfferId,
            validCommunityIdentityId,
            purchasedAtUtc,
            cancellationToken);
    }

    private static DateTimeOffset CanonicalizeToPostgreSqlMicroseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var excessTicks = utc.Ticks % TimeSpan.TicksPerMicrosecond;
        return excessTicks == 0 ? utc : utc.AddTicks(-excessTicks);
    }
}
