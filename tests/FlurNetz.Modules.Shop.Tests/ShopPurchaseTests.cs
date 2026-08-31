using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopPurchaseTests
{
    [Fact]
    public async Task PurchaseUseCaseGeneratesPurchaseIdCanonicalizesTimestampAndDelegates()
    {
        var executor = new RecordingPurchaseExecutor();
        var clock = new FixedClock(
            new DateTimeOffset(2026, 8, 31, 18, 15, 0, TimeSpan.FromHours(2)).AddTicks(7));
        var useCase = new PurchaseShopOffer(executor, clock);
        var requestId = ShopPurchaseRequestId.New();
        var shopOfferId = ShopOfferId.New();
        var identityId = CommunityIdentityId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(
            requestId,
            shopOfferId,
            identityId,
            cancellationSource.Token);

        Assert.Equal(requestId, executor.RequestId);
        Assert.Equal(shopOfferId, executor.ShopOfferId);
        Assert.Equal(identityId, executor.CommunityIdentityId);
        Assert.NotEqual(Guid.Empty, executor.PurchaseId.Value);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 31, 16, 15, 0, TimeSpan.Zero),
            executor.PurchasedAtUtc);
        Assert.Equal(0, executor.PurchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(cancellationSource.Token, executor.CancellationToken);
        Assert.Equal(executor.PurchaseId, result.Id);
    }

    [Fact]
    public void ShopPurchaseRehydratesImmutableHistoricalSnapshot()
    {
        var purchase = ShopPurchase.Rehydrate(
            ShopPurchaseId.New(),
            ShopOfferId.New(),
            CommunityIdentityId.New(),
            ItemDefinitionId.New(),
            ShopPrice.Create(42),
            new DateTimeOffset(2026, 8, 31, 16, 15, 0, TimeSpan.Zero));

        Assert.Equal(42, purchase.PricePaid.Value);
        Assert.All(typeof(ShopPurchase).GetProperties(), property => Assert.Null(property.GetSetMethod()));
    }

    [Fact]
    public void PurchaseCompletedEventHasStableWireIdentityAndRejectsInvalidValues()
    {
        var timestamp = new DateTimeOffset(2026, 8, 31, 16, 15, 0, TimeSpan.Zero);
        var integrationEvent = new ShopPurchaseCompletedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            timestamp);

        Assert.Equal("shop.purchase-completed", ShopPurchaseCompletedIntegrationEvent.MessageType);
        Assert.Equal(1, ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        Assert.Equal(timestamp, integrationEvent.PurchasedAtUtc);
        Assert.Throws<ArgumentException>(() => new ShopPurchaseCompletedIntegrationEvent(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            timestamp));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShopPurchaseCompletedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            -1,
            timestamp));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class RecordingPurchaseExecutor : IShopPurchaseExecutor
    {
        public ShopPurchaseRequestId RequestId { get; private set; }
        public ShopPurchaseId PurchaseId { get; private set; }
        public ShopOfferId ShopOfferId { get; private set; }
        public CommunityIdentityId CommunityIdentityId { get; private set; }
        public DateTimeOffset PurchasedAtUtc { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ShopPurchase> ExecuteAsync(
            ShopPurchaseRequestId requestId,
            ShopPurchaseId purchaseId,
            ShopOfferId shopOfferId,
            CommunityIdentityId communityIdentityId,
            DateTimeOffset purchasedAtUtc,
            CancellationToken cancellationToken = default)
        {
            RequestId = requestId;
            PurchaseId = purchaseId;
            ShopOfferId = shopOfferId;
            CommunityIdentityId = communityIdentityId;
            PurchasedAtUtc = purchasedAtUtc;
            CancellationToken = cancellationToken;

            return Task.FromResult(ShopPurchase.Create(
                purchaseId,
                shopOfferId,
                communityIdentityId,
                ItemDefinitionId.New(),
                ShopPrice.Zero,
                purchasedAtUtc));
        }
    }
}
