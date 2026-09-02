using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopApplicationTests
{
    [Fact]
    public async Task CreateGeneratesNewIdPersistsOfferAndReturnsDisabledOffer()
    {
        var store = new InMemoryShopOfferStore();
        var useCase = new CreateShopOffer(store);
        var itemDefinitionId = ItemDefinitionId.New();
        var cancellationToken = new CancellationTokenSource().Token;

        var offer = await useCase.ExecuteAsync(
            itemDefinitionId,
            "  Angebot  ",
            "  Beschreibung  ",
            ShopPrice.Create(7),
            AvailabilityWindow.Create(null, null),
            2,
            cancellationToken,
            sortOrder: 7);

        Assert.NotEqual(Guid.Empty, offer.Id.Value);
        Assert.Equal(offer, store.AddedOffer);
        Assert.Equal(itemDefinitionId, offer.ItemDefinitionId);
        Assert.Equal("Angebot", offer.DisplayName);
        Assert.Equal("Beschreibung", offer.Description);
        Assert.Equal(ShopPrice.Create(7), offer.Price);
        Assert.False(offer.IsEnabled);
        Assert.False(offer.IsArchived);
        Assert.Equal(2, offer.PurchaseLimitPerIdentity);
        Assert.Equal(7, offer.SortOrder);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task CreateDefaultsSortOrderToZero()
    {
        var store = new InMemoryShopOfferStore();

        var offer = await new CreateShopOffer(store).ExecuteAsync(
            ItemDefinitionId.New(),
            "Angebot",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, offer.SortOrder);
    }

    [Fact]
    public async Task CatalogUseCasesDelegateToStoreAndDomainMethods()
    {
        var id = ShopOfferId.New();
        var store = new InMemoryShopOfferStore
        {
            Offer = ShopOffer.Create(id, ItemDefinitionId.New(), "Alt", "Alt", ShopPrice.Create(1))
        };
        var token = new CancellationTokenSource().Token;

        Assert.True(await new RenameShopOffer(store).ExecuteAsync(id, "Neu", token));
        Assert.True(await new ChangeShopOfferDescription(store).ExecuteAsync(id, null, token));
        Assert.True(await new ChangeShopOfferPrice(store).ExecuteAsync(id, ShopPrice.Create(2), token));
        Assert.True(await new ChangeShopOfferAvailability(store).ExecuteAsync(
            id,
            AvailabilityWindow.Create(
                new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                null),
            token));
        Assert.True(await new ChangeShopOfferPurchaseLimit(store).ExecuteAsync(id, 1, token));
        Assert.True(await new ChangeShopOfferSortOrder(store).ExecuteAsync(id, 10, token));
        Assert.False(await new ChangeShopOfferSortOrder(store).ExecuteAsync(id, 10, token));
        Assert.True(await new EnableShopOffer(store).ExecuteAsync(id, token));
        Assert.True(await new DisableShopOffer(store).ExecuteAsync(id, token));
        Assert.True(await new ArchiveShopOffer(store).ExecuteAsync(id, token));
        Assert.False(await new ArchiveShopOffer(store).ExecuteAsync(id, token));

        Assert.Equal("Neu", store.Offer!.DisplayName);
        Assert.Null(store.Offer.Description);
        Assert.Equal(ShopPrice.Create(2), store.Offer.Price);
        Assert.Equal(1, store.Offer.PurchaseLimitPerIdentity);
        Assert.Equal(10, store.Offer.SortOrder);
        Assert.False(store.Offer.IsEnabled);
        Assert.True(store.Offer.IsArchived);
        Assert.Equal(id, store.LastExecutedId);
        Assert.Equal(token, store.LastCancellationToken);
    }

    [Fact]
    public async Task GetAndListPassThroughStoreResults()
    {
        var offer = ShopOffer.Create(ShopOfferId.New(), ItemDefinitionId.New(), "Angebot");
        var list = Array.AsReadOnly(new[] { offer });
        var store = new InMemoryShopOfferStore { Offer = offer, ListResult = list };

        Assert.Same(offer, await new GetShopOffer(store).ExecuteAsync(
            offer.Id,
            TestContext.Current.CancellationToken));
        Assert.Same(list, await new ListShopOffers(store).ExecuteAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnknownOfferIdIsPropagatedForAtomicMutation()
    {
        var store = new InMemoryShopOfferStore();

        await Assert.ThrowsAsync<ShopOfferNotFoundException>(() =>
            new EnableShopOffer(store).ExecuteAsync(
                ShopOfferId.New(),
                TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ShopOfferNotFoundException>(() =>
            new ChangeShopOfferSortOrder(store).ExecuteAsync(
                ShopOfferId.New(),
                1,
                TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ShopOfferNotFoundException>(() =>
            new ArchiveShopOffer(store).ExecuteAsync(
                ShopOfferId.New(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CatalogUseCaseConstructorsRejectNullStore()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateShopOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new GetShopOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new ListShopOffers(null!));
        Assert.Throws<ArgumentNullException>(() => new RenameShopOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeShopOfferDescription(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeShopOfferPrice(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeShopOfferAvailability(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeShopOfferPurchaseLimit(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeShopOfferSortOrder(null!));
        Assert.Throws<ArgumentNullException>(() => new EnableShopOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new DisableShopOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new ArchiveShopOffer(null!));
    }

    [Fact]
    public void NotFoundExceptionKeepsItsValidId()
    {
        var id = ShopOfferId.New();

        var exception = new ShopOfferNotFoundException(id);

        Assert.Equal(id, exception.ShopOfferId);
        Assert.Contains(id.Value.ToString(), exception.Message);
        Assert.Contains("nicht gefunden", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentException>(() => new ShopOfferNotFoundException(default));
    }

    private sealed class InMemoryShopOfferStore : IShopOfferStore
    {
        public ShopOffer? Offer { get; init; }

        public IReadOnlyList<ShopOffer> ListResult { get; init; } =
            Array.AsReadOnly(Array.Empty<ShopOffer>());

        public ShopOffer? AddedOffer { get; private set; }

        public ShopOfferId? LastExecutedId { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task AddAsync(ShopOffer offer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offer);
            AddedOffer = offer;
            LastCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<ShopOffer?> GetAsync(
            ShopOfferId shopOfferId,
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Offer);
        }

        public Task<IReadOnlyList<ShopOffer>> ListAsync(CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(ListResult);
        }

        public Task<bool> ExecuteAsync(
            ShopOfferId shopOfferId,
            Func<ShopOffer, bool> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            LastExecutedId = shopOfferId;
            LastCancellationToken = cancellationToken;

            if (Offer is null)
            {
                throw new ShopOfferNotFoundException(shopOfferId);
            }

            return Task.FromResult(operation(Offer));
        }
    }
}
