using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopPurchaseHistoryApplicationTests
{
    [Fact]
    public async Task GetShopPurchaseDelegatesValidatedIdAndCancellationToken()
    {
        var purchase = CreatePurchase();
        var store = new FakeHistoryStore { Purchase = purchase };
        var cancellationToken = new CancellationTokenSource().Token;

        var result = await new GetShopPurchase(store).ExecuteAsync(
            purchase.Id,
            cancellationToken);

        Assert.Same(purchase, result);
        Assert.Equal(purchase.Id, store.LastGetId);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task UnknownShopPurchaseIdReturnsNull()
    {
        var store = new FakeHistoryStore();

        var result = await new GetShopPurchase(store).ExecuteAsync(
            ShopPurchaseId.New(),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task EmptyHistoryReturnsEmptyPageWithoutCursor()
    {
        var store = new FakeHistoryStore();
        var identityId = CommunityIdentityId.New();

        var page = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            identityId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
        Assert.Equal(identityId, store.LastIdentityId);
        Assert.Equal(ListShopPurchasesForIdentity.DefaultPageSize + 1, store.LastTake);
    }

    [Fact]
    public async Task DefaultPageSizeReadsOneAdditionalPurchase()
    {
        var store = new FakeHistoryStore();

        _ = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            CommunityIdentityId.New(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(51, store.LastTake);
    }

    [Fact]
    public async Task PageSizeOneIsAccepted()
    {
        var store = new FakeHistoryStore();

        var page = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            CommunityIdentityId.New(),
            pageSize: 1,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(2, store.LastTake);
    }

    [Fact]
    public async Task PageSizeOneHundredIsAccepted()
    {
        var store = new FakeHistoryStore();

        var page = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            CommunityIdentityId.New(),
            pageSize: 100,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(101, store.LastTake);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PageSizeOutsideTheApplicationBoundsIsRejected(int pageSize)
    {
        var store = new FakeHistoryStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListShopPurchasesForIdentity(store).ExecuteAsync(
                CommunityIdentityId.New(),
                pageSize: pageSize,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(store.ListWasCalled);
    }

    [Fact]
    public async Task AdditionalPurchaseIsNotReturnedAndNextCursorUsesLastReturnedPurchase()
    {
        var identityId = CommunityIdentityId.New();
        var first = CreatePurchase(identityId, new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero));
        var second = CreatePurchase(identityId, new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero));
        var additional = CreatePurchase(identityId, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new FakeHistoryStore
        {
            History = [first, second, additional]
        };

        var page = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            identityId,
            pageSize: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([first, second], page.Items);
        Assert.Equal(
            new ShopPurchaseHistoryCursor(identityId, second.PurchasedAtUtc, second.Id),
            page.NextCursor);
        Assert.Equal(3, store.LastTake);
    }

    [Fact]
    public async Task NextCursorIsNullWhenNoAdditionalPurchaseExists()
    {
        var identityId = CommunityIdentityId.New();
        var first = CreatePurchase(identityId);
        var second = CreatePurchase(
            identityId,
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new FakeHistoryStore { History = [first, second] };

        var page = await new ListShopPurchasesForIdentity(store).ExecuteAsync(
            identityId,
            pageSize: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([first, second], page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task CursorForAnotherIdentityIsRejectedBeforeStoreAccess()
    {
        var requestedIdentityId = CommunityIdentityId.New();
        var cursor = new ShopPurchaseHistoryCursor(
            CommunityIdentityId.New(),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            ShopPurchaseId.New());
        var store = new FakeHistoryStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListShopPurchasesForIdentity(store).ExecuteAsync(
                requestedIdentityId,
                cursor,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(store.ListWasCalled);
    }

    [Fact]
    public void CursorRejectsEmptyIdentityAndPurchaseIds()
    {
        var validTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new ShopPurchaseHistoryCursor(
            default,
            validTime,
            ShopPurchaseId.New()));
        Assert.Throws<ArgumentException>(() => new ShopPurchaseHistoryCursor(
            CommunityIdentityId.New(),
            validTime,
            default));
    }

    [Fact]
    public void CursorRejectsNonUtcTime()
    {
        Assert.Throws<ArgumentException>(() => new ShopPurchaseHistoryCursor(
            CommunityIdentityId.New(),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(1)),
            ShopPurchaseId.New()));
    }

    [Fact]
    public void CursorRejectsSubMicrosecondPrecision()
    {
        Assert.Throws<ArgumentException>(() => new ShopPurchaseHistoryCursor(
            CommunityIdentityId.New(),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1),
            ShopPurchaseId.New()));
    }

    [Fact]
    public void HistoryUseCaseConstructorRejectsNullStore()
    {
        Assert.Throws<ArgumentNullException>(() => new GetShopPurchase(null!));
        Assert.Throws<ArgumentNullException>(() => new ListShopPurchasesForIdentity(null!));
    }

    private static ShopPurchase CreatePurchase(
        CommunityIdentityId? identityId = null,
        DateTimeOffset? purchasedAtUtc = null)
    {
        return ShopPurchase.Create(
            ShopPurchaseId.New(),
            ShopOfferId.New(),
            identityId ?? CommunityIdentityId.New(),
            ItemDefinitionId.New(),
            ShopPrice.Create(7),
            purchasedAtUtc
                ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeHistoryStore : IShopPurchaseHistoryStore
    {
        public ShopPurchase? Purchase { get; init; }

        public IReadOnlyList<ShopPurchase> History { get; init; } =
            Array.AsReadOnly(Array.Empty<ShopPurchase>());

        public ShopPurchaseId? LastGetId { get; private set; }

        public CommunityIdentityId? LastIdentityId { get; private set; }

        public ShopPurchaseHistoryCursor? LastCursor { get; private set; }

        public int LastTake { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public bool ListWasCalled { get; private set; }

        public Task<ShopPurchase?> GetAsync(
            ShopPurchaseId shopPurchaseId,
            CancellationToken cancellationToken = default)
        {
            LastGetId = shopPurchaseId;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Purchase);
        }

        public Task<IReadOnlyList<ShopPurchase>> ListForIdentityAsync(
            CommunityIdentityId communityIdentityId,
            ShopPurchaseHistoryCursor? cursor,
            int take,
            CancellationToken cancellationToken = default)
        {
            ListWasCalled = true;
            LastIdentityId = communityIdentityId;
            LastCursor = cursor;
            LastTake = take;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(History);
        }
    }
}
