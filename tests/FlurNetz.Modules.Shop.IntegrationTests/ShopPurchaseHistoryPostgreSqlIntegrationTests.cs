using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Shop.IntegrationTests;

/// <summary>
/// Prüft die read-only Shop-Kaufhistorie Ende zu Ende gegen echtes PostgreSQL.
/// </summary>
public sealed class ShopPurchaseHistoryPostgreSqlIntegrationTests(ShopPostgreSqlFixture database)
    : IClassFixture<ShopPostgreSqlFixture>
{
    [Fact]
    public async Task GetRehydratesEveryPersistedPurchaseFieldAndUnknownIdReturnsNull()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        var purchaseId = ShopPurchaseId.New();
        var purchasedAtUtc = Utc(16, 15, 0);
        await CreateOfferAsync(factory, offerId);
        var expected = await InsertPurchaseAsync(
            factory,
            purchaseId,
            offerId,
            identityId,
            itemDefinitionId,
            pricePaid: 42,
            purchasedAtUtc);

        var store = new ShopPurchaseHistoryStore(factory);
        var loaded = await store.GetAsync(purchaseId, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(expected.Id, loaded!.Id);
        Assert.Equal(expected.ShopOfferId, loaded.ShopOfferId);
        Assert.Equal(expected.CommunityIdentityId, loaded.CommunityIdentityId);
        Assert.Equal(expected.ItemDefinitionId, loaded.ItemDefinitionId);
        Assert.Equal(expected.PricePaid, loaded.PricePaid);
        Assert.Equal(expected.PurchasedAtUtc, loaded.PurchasedAtUtc);
        Assert.Null(await store.GetAsync(ShopPurchaseId.New(), TestToken));
    }

    [Fact]
    public async Task HistoryContainsOnlyPurchasesOfRequestedIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var requestedIdentityId = CommunityIdentityId.New();
        var otherIdentityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        await CreateOfferAsync(factory, offerId);
        var requestedPurchase = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.New(),
            offerId,
            requestedIdentityId,
            ItemDefinitionId.New(),
            pricePaid: 1,
            Utc(12, 0, 0));
        _ = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.New(),
            offerId,
            otherIdentityId,
            ItemDefinitionId.New(),
            pricePaid: 2,
            Utc(13, 0, 0));

        var page = await new ListShopPurchasesForIdentity(new ShopPurchaseHistoryStore(factory))
            .ExecuteAsync(
                requestedIdentityId,
                pageSize: 10,
                cancellationToken: TestToken);

        Assert.Equal([requestedPurchase.Id], page.Items.Select(purchase => purchase.Id).ToArray());
        Assert.All(page.Items, purchase => Assert.Equal(requestedIdentityId, purchase.CommunityIdentityId));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task HistoryIsNewestFirstAndUsesPurchaseIdDescendingForEqualTimestamps()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        await CreateOfferAsync(factory, offerId);
        var sameTime = Utc(12, 0, 0);
        var oldest = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 1,
            sameTime);
        var newestSameTime = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000003")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 3,
            sameTime);
        var middleTime = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 2,
            Utc(11, 0, 0));

        var page = await new ListShopPurchasesForIdentity(new ShopPurchaseHistoryStore(factory))
            .ExecuteAsync(
                identityId,
                pageSize: 10,
                cancellationToken: TestToken);

        Assert.Equal(
            [newestSameTime.Id, oldest.Id, middleTime.Id],
            page.Items.Select(purchase => purchase.Id).ToArray());
    }

    [Fact]
    public async Task KeysetPaginationAcrossEqualTimestampsHasNoDuplicatesOrSkippedPurchases()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        await CreateOfferAsync(factory, offerId);
        var purchasedAtUtc = Utc(12, 0, 0);
        var expectedIds = new List<ShopPurchaseId>();
        for (var number = 1; number <= 5; number++)
        {
            var purchaseId = ShopPurchaseId.Create(
                Guid.Parse($"00000000-0000-0000-0000-{number:D12}"));
            expectedIds.Add(purchaseId);
            _ = await InsertPurchaseAsync(
                factory,
                purchaseId,
                offerId,
                identityId,
                ItemDefinitionId.New(),
                pricePaid: number,
                purchasedAtUtc);
        }

        var useCase = new ListShopPurchasesForIdentity(new ShopPurchaseHistoryStore(factory));
        var actualIds = new List<ShopPurchaseId>();
        ShopPurchaseHistoryCursor? cursor = null;

        for (var pageNumber = 0; pageNumber < 5; pageNumber++)
        {
            var page = await useCase.ExecuteAsync(
                identityId,
                cursor,
                pageSize: 2,
                cancellationToken: TestToken);
            actualIds.AddRange(page.Items.Select(purchase => purchase.Id));
            cursor = page.NextCursor;

            if (cursor is null)
            {
                break;
            }
        }

        Assert.Equal(
            expectedIds.OrderByDescending(id => id.Value).ToArray(),
            actualIds.ToArray());
        Assert.Equal(actualIds.Count, actualIds.Distinct().Count());
        Assert.Null(cursor);
    }

    [Fact]
    public async Task LastPageHasNoNextCursorAndEmptyHistoryHasNoItemsOrCursor()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        await CreateOfferAsync(factory, offerId);
        _ = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.New(),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 0,
            Utc(12, 0, 0));

        var useCase = new ListShopPurchasesForIdentity(new ShopPurchaseHistoryStore(factory));
        var lastPage = await useCase.ExecuteAsync(
            identityId,
            pageSize: 2,
            cancellationToken: TestToken);
        var emptyPage = await useCase.ExecuteAsync(
            CommunityIdentityId.New(),
            pageSize: 2,
            cancellationToken: TestToken);

        Assert.Single(lastPage.Items);
        Assert.Null(lastPage.NextCursor);
        Assert.Empty(emptyPage.Items);
        Assert.Null(emptyPage.NextCursor);
    }

    [Fact]
    public async Task NewerPurchaseBetweenPagesDoesNotMoveTheIssuedCursorBackwards()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareHistoryDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        var offerId = ShopOfferId.New();
        await CreateOfferAsync(factory, offerId);
        var firstPagePurchase = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000003")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 3,
            Utc(13, 0, 0));
        var secondPagePurchase = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 2,
            Utc(12, 0, 0));
        _ = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 1,
            Utc(11, 0, 0));

        var useCase = new ListShopPurchasesForIdentity(new ShopPurchaseHistoryStore(factory));
        var firstPage = await useCase.ExecuteAsync(
            identityId,
            pageSize: 1,
            cancellationToken: TestToken);
        var issuedCursor = firstPage.NextCursor;

        var newerPurchase = await InsertPurchaseAsync(
            factory,
            ShopPurchaseId.Create(Guid.Parse("00000000-0000-0000-0000-000000000004")),
            offerId,
            identityId,
            ItemDefinitionId.New(),
            pricePaid: 4,
            Utc(14, 0, 0));

        var secondPage = await useCase.ExecuteAsync(
            identityId,
            issuedCursor,
            pageSize: 1,
            cancellationToken: TestToken);

        Assert.Equal(firstPagePurchase.Id, firstPage.Items.Single().Id);
        Assert.Equal(
            new ShopPurchaseHistoryCursor(
                identityId,
                firstPagePurchase.PurchasedAtUtc,
                firstPagePurchase.Id),
            issuedCursor);
        Assert.Equal(secondPagePurchase.Id, secondPage.Items.Single().Id);
        Assert.DoesNotContain(
            secondPage.Items,
            purchase => purchase.Id == newerPurchase.Id);
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static async Task PrepareHistoryDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new ShopMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM shop_purchase_requests;
                DELETE FROM shop_purchase_guards;
                DELETE FROM shop_purchases;
                DELETE FROM shop_offers;
                """,
                cancellationToken: TestToken));
    }

    private static async Task CreateOfferAsync(
        PostgreSqlConnectionFactory factory,
        ShopOfferId offerId)
    {
        await new ShopOfferStore(factory).AddAsync(
            ShopOffer.Create(
                offerId,
                ItemDefinitionId.New(),
                "History-Test-Angebot",
                null,
                ShopPrice.Zero,
                AvailabilityWindow.Create(null, null)),
            TestToken);
    }

    private static async Task<ShopPurchase> InsertPurchaseAsync(
        PostgreSqlConnectionFactory factory,
        ShopPurchaseId purchaseId,
        ShopOfferId offerId,
        CommunityIdentityId identityId,
        ItemDefinitionId itemDefinitionId,
        long pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        var purchase = ShopPurchase.Create(
            purchaseId,
            offerId,
            identityId,
            itemDefinitionId,
            ShopPrice.Create(pricePaid),
            purchasedAtUtc);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO shop_purchases
                    (id, shop_offer_id, community_identity_id,
                     purchased_inventory_item_definition_id, price_paid, purchased_at)
                VALUES
                    (@Id, @ShopOfferId, @CommunityIdentityId,
                     @ItemDefinitionId, @PricePaid, @PurchasedAt);
                """,
                new
                {
                    Id = purchase.Id.Value,
                    ShopOfferId = purchase.ShopOfferId.Value,
                    CommunityIdentityId = purchase.CommunityIdentityId.Value,
                    ItemDefinitionId = purchase.ItemDefinitionId.Value,
                    PricePaid = purchase.PricePaid.Value,
                    PurchasedAt = purchase.PurchasedAtUtc
                },
                cancellationToken: TestToken));

        return purchase;
    }

    private static DateTimeOffset Utc(int hour, int minute, int second) =>
        new(2026, 8, 31, hour, minute, second, TimeSpan.Zero);

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
