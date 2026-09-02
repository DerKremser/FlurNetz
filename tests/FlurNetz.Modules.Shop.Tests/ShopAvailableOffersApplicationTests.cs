using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopAvailableOffersApplicationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAvailableOfferReturnsEnabledOfferWithinWindow()
    {
        var offer = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(Now, Now.AddHours(1)));
        var store = new FakeOfferStore { Offer = offer };

        var result = await new GetAvailableShopOffer(store, new FixedClock(Now))
            .ExecuteAsync(offer.Id, TestToken);

        Assert.Same(offer, result);
    }

    [Fact]
    public async Task GetAvailableOfferReturnsNullForDisabledOffer()
    {
        var offer = CreateOffer(
            isEnabled: false,
            availability: AvailabilityWindow.Create(null, null));
        var store = new FakeOfferStore { Offer = offer };

        var result = await new GetAvailableShopOffer(store, new FixedClock(Now))
            .ExecuteAsync(offer.Id, TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvailableOfferReturnsNullForArchivedOffer()
    {
        var offer = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(null, null));
        offer.Archive();
        var store = new FakeOfferStore { Offer = offer };

        var result = await new GetAvailableShopOffer(store, new FixedClock(Now))
            .ExecuteAsync(offer.Id, TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvailableOfferUsesInclusiveStartAndExclusiveEnd()
    {
        var from = Now.AddHours(-1);
        var until = Now;
        var offer = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(from, until));
        var store = new FakeOfferStore { Offer = offer };

        Assert.Null(await new GetAvailableShopOffer(store, new FixedClock(from.AddTicks(-1)))
            .ExecuteAsync(offer.Id, TestToken));
        Assert.Same(offer, await new GetAvailableShopOffer(store, new FixedClock(from))
            .ExecuteAsync(offer.Id, TestToken));
        Assert.Null(await new GetAvailableShopOffer(store, new FixedClock(until))
            .ExecuteAsync(offer.Id, TestToken));
        Assert.Null(await new GetAvailableShopOffer(store, new FixedClock(until.AddTicks(1)))
            .ExecuteAsync(offer.Id, TestToken));
    }

    [Fact]
    public async Task GetAvailableOfferReturnsNullBeforeWindowAndAfterWindow()
    {
        var offer = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(Now, Now.AddHours(1)));
        var store = new FakeOfferStore { Offer = offer };

        Assert.Null(await new GetAvailableShopOffer(store, new FixedClock(Now.AddTicks(-1)))
            .ExecuteAsync(offer.Id, TestToken));
        Assert.Null(await new GetAvailableShopOffer(store, new FixedClock(Now.AddHours(1)))
            .ExecuteAsync(offer.Id, TestToken));
    }

    [Fact]
    public async Task GetAvailableOfferReturnsNullForUnknownOffer()
    {
        var store = new FakeOfferStore();

        var result = await new GetAvailableShopOffer(store, new FixedClock(Now))
            .ExecuteAsync(ShopOfferId.New(), TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvailableOfferForwardsCancellationToken()
    {
        var offer = CreateOffer(isEnabled: true);
        var store = new FakeOfferStore { Offer = offer };
        using var cancellation = new CancellationTokenSource();

        _ = await new GetAvailableShopOffer(store, new FixedClock(Now))
            .ExecuteAsync(offer.Id, cancellation.Token);

        Assert.Equal(cancellation.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task ListAvailableOffersKeepsCatalogOrderAndFiltersByOneCurrentTime()
    {
        var first = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(null, null));
        var disabled = CreateOffer(
            isEnabled: false,
            availability: AvailabilityWindow.Create(null, null));
        var future = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(Now.AddHours(1), null));
        var last = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(null, Now.AddHours(1)));
        var archived = CreateOffer(
            isEnabled: true,
            availability: AvailabilityWindow.Create(null, null));
        archived.Archive();
        var store = new FakeOfferStore
        {
            Offers = [first, disabled, future, archived, last]
        };
        var clock = new CountingClock(Now);

        var result = await new ListAvailableShopOffers(store, clock).ExecuteAsync(TestToken);

        Assert.Equal([first, last], result);
        Assert.Equal(1, clock.ReadCount);
        Assert.Equal(TestToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task ListAvailableOffersSupportsEmptyCatalog()
    {
        var result = await new ListAvailableShopOffers(
                new FakeOfferStore(),
                new FixedClock(Now))
            .ExecuteAsync(TestToken);

        Assert.Empty(result);
    }

    [Fact]
    public void AvailableOfferConstructorsRejectNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new GetAvailableShopOffer(null!, new FixedClock(Now)));
        Assert.Throws<ArgumentNullException>(() => new GetAvailableShopOffer(new FakeOfferStore(), null!));
        Assert.Throws<ArgumentNullException>(() => new ListAvailableShopOffers(null!, new FixedClock(Now)));
        Assert.Throws<ArgumentNullException>(() => new ListAvailableShopOffers(new FakeOfferStore(), null!));
    }

    private static ShopOffer CreateOffer(
        bool isEnabled,
        AvailabilityWindow availability = default)
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            "Beschreibung",
            ShopPrice.Create(7),
            availability);
        if (isEnabled)
        {
            offer.Enable();
        }

        return offer;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class CountingClock(DateTimeOffset utcNow) : IClock
    {
        public int ReadCount { get; private set; }

        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCount++;
                return utcNow;
            }
        }
    }

    private sealed class FakeOfferStore : IShopOfferStore
    {
        public ShopOffer? Offer { get; init; }

        public IReadOnlyList<ShopOffer> Offers { get; init; } =
            Array.AsReadOnly(Array.Empty<ShopOffer>());

        public CancellationToken LastCancellationToken { get; private set; }

        public Task AddAsync(ShopOffer offer, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ShopOffer?> GetAsync(
            ShopOfferId shopOfferId,
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Offer);
        }

        public Task<IReadOnlyList<ShopOffer>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Offers);
        }

        public Task<bool> ExecuteAsync(
            ShopOfferId shopOfferId,
            Func<ShopOffer, bool> operation,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
