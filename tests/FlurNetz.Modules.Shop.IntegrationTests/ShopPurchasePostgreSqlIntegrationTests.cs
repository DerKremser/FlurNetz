using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
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
/// Prüft den ersten Shop-Purchase-Slice Ende zu Ende innerhalb einer echten PostgreSQL-Transaktion.
/// </summary>
public sealed class ShopPurchasePostgreSqlIntegrationTests(ShopPostgreSqlFixture database)
    : IClassFixture<ShopPostgreSqlFixture>
{
    private static readonly DateTimeOffset PurchaseTime =
        new(2026, 8, 31, 16, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulPurchaseCommitsDebitInventoryPurchaseRequestAndOutboxTogether()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(
            factory,
            itemDefinitionId,
            price: 25,
            purchaseLimit: 2);
        var requestId = ShopPurchaseRequestId.New();
        var useCase = CreateUseCase(factory, PurchaseTime);

        var purchase = await useCase.ExecuteAsync(
            requestId,
            offer.Id,
            identityId,
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var balance = await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @IdentityId;",
                new { IdentityId = identityId.Value },
                cancellationToken: TestToken));
        var quantity = await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                """
                SELECT quantity
                FROM community_inventory_entries
                WHERE community_identity_id = @IdentityId
                  AND item_definition_id = @ItemDefinitionId;
                """,
                new
                {
                    IdentityId = identityId.Value,
                    ItemDefinitionId = itemDefinitionId.Value
                },
                cancellationToken: TestToken));
        var persistedPurchase = await connection.QuerySingleAsync<PurchaseRow>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    shop_offer_id AS ShopOfferId,
                    community_identity_id AS CommunityIdentityId,
                    purchased_inventory_item_definition_id AS ItemDefinitionId,
                    price_paid AS PricePaid,
                    purchased_at AS PurchasedAt
                FROM shop_purchases
                WHERE id = @Id;
                """,
                new { Id = purchase.Id.Value },
                cancellationToken: TestToken));
        var requestMapping = await connection.QuerySingleAsync<RequestRow>(
            new CommandDefinition(
                """
                SELECT
                    request_id AS RequestId,
                    shop_purchase_id AS ShopPurchaseId,
                    shop_offer_id AS ShopOfferId,
                    community_identity_id AS CommunityIdentityId
                FROM shop_purchase_requests
                WHERE request_id = @RequestId;
                """,
                new { RequestId = requestId.Value },
                cancellationToken: TestToken));
        var outbox = await connection.QuerySingleAsync<OutboxRow>(
            new CommandDefinition(
                """
                SELECT
                    message_type AS MessageType,
                    schema_version AS SchemaVersion,
                    occurred_at_utc AS OccurredAtUtc,
                    correlation_id AS CorrelationId
                FROM flurnetz_messaging.outbox_messages;
                """,
                cancellationToken: TestToken));

        Assert.Equal(75, balance);
        Assert.Equal(1, quantity);
        Assert.Equal(purchase.Id.Value, persistedPurchase.Id);
        Assert.Equal(offer.Id.Value, persistedPurchase.ShopOfferId);
        Assert.Equal(identityId.Value, persistedPurchase.CommunityIdentityId);
        Assert.Equal(itemDefinitionId.Value, persistedPurchase.ItemDefinitionId);
        Assert.Equal(25, persistedPurchase.PricePaid);
        Assert.Equal(PurchaseTime, persistedPurchase.PurchasedAt);
        Assert.Equal(requestId.Value, requestMapping.RequestId);
        Assert.Equal(purchase.Id.Value, requestMapping.ShopPurchaseId);
        Assert.Equal(1L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(ShopPurchaseCompletedIntegrationEvent.MessageType, outbox.MessageType);
        Assert.Equal(ShopPurchaseCompletedIntegrationEvent.SchemaVersion, outbox.SchemaVersion);
        Assert.Equal(PurchaseTime, outbox.OccurredAtUtc);
        Assert.Equal(requestId.Value.ToString("D"), outbox.CorrelationId);
    }

    [Fact]
    public async Task ConcurrentDuplicateRequestReturnsSamePurchaseAndAppliesEffectsOnce()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 20, purchaseLimit: 5);
        var requestId = ShopPurchaseRequestId.New();
        var useCase = CreateUseCase(factory, PurchaseTime);

        var results = await Task.WhenAll(
            useCase.ExecuteAsync(requestId, offer.Id, identityId, TestToken),
            useCase.ExecuteAsync(requestId, offer.Id, identityId, TestToken));

        Assert.Equal(results[0].Id, results[1].Id);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(80, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task ReusingRequestForDifferentOfferIsRejectedWithoutSecondEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var firstItem = ItemDefinitionId.New();
        var secondItem = ItemDefinitionId.New();
        var firstOffer = await CreateEnabledOfferAsync(factory, firstItem, price: 10);
        var secondOffer = await CreateEnabledOfferAsync(factory, secondItem, price: 15);
        var requestId = ShopPurchaseRequestId.New();
        var useCase = CreateUseCase(factory, PurchaseTime);

        var firstPurchase = await useCase.ExecuteAsync(
            requestId,
            firstOffer.Id,
            identityId,
            TestToken);

        var exception = await Assert.ThrowsAsync<ShopPurchaseIdempotencyConflictException>(
            () => useCase.ExecuteAsync(
                requestId,
                secondOffer.Id,
                identityId,
                TestToken));

        Assert.Equal(requestId, exception.RequestId);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(90, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, firstItem));
        Assert.Null(await ReadQuantityOrNullAsync(connection, identityId, secondItem));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1L, await CountOutboxAsync(connection));

        var storedPurchaseId = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                "SELECT shop_purchase_id FROM shop_purchase_requests WHERE request_id = @RequestId;",
                new { RequestId = requestId.Value },
                cancellationToken: TestToken));
        Assert.Equal(firstPurchase.Id.Value, storedPurchaseId);
    }

    [Fact]
    public async Task ConcurrentPurchasesRespectPerIdentityLimit()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(
            factory,
            itemDefinitionId,
            price: 10,
            purchaseLimit: 1);
        var useCase = CreateUseCase(factory, PurchaseTime);

        var attempts = await Task.WhenAll(
            CaptureAsync(() => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken)),
            CaptureAsync(() => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken)));

        Assert.Equal(1, attempts.Count(attempt => attempt.Purchase is not null));
        Assert.Single(attempts.Where(attempt => attempt.Error is ShopPurchaseLimitExceededException));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(90, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task InsufficientBalanceRollsBackRequestGuardInventoryPurchaseAndOutbox()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 10);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(
            factory,
            itemDefinitionId,
            price: 50,
            purchaseLimit: 1);
        var useCase = CreateUseCase(factory, PurchaseTime);

        await Assert.ThrowsAsync<InsufficientEconomyBalanceException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(10, await ReadBalanceAsync(connection, identityId));
        Assert.Null(await ReadQuantityOrNullAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static PurchaseShopOffer CreateUseCase(
        PostgreSqlConnectionFactory factory,
        DateTimeOffset now)
    {
        var clock = new FixedClock(now);
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);

        var publisher = new PostgreSqlOutboxPublisher(
            new IntegrationEventJsonSerializer(registry),
            clock);
        var executor = new PostgreSqlShopPurchaseExecutor(
            factory,
            new CommunityIdentityExistence(),
            new EconomyBalanceDebit(new CommunityEconomyStore(factory)),
            new InventoryQuantityGrant(new CommunityInventoryStore(factory)),
            publisher);

        return new PurchaseShopOffer(executor, clock);
    }

    private static async Task PreparePurchaseDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        var migrations = new MessagingMigrationSource().GetMigrations()
            .Concat(new IdentityMigrationSource().GetMigrations())
            .Concat(new EconomyMigrationSource().GetMigrations())
            .Concat(new InventoryMigrationSource().GetMigrations())
            .Concat(new ShopMigrationSource().GetMigrations());

        await new MigrationRunner(factory, new MigrationSource(migrations))
            .RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM flurnetz_messaging.outbox_messages;
                DELETE FROM shop_purchase_requests;
                DELETE FROM shop_purchase_guards;
                DELETE FROM shop_purchases;
                DELETE FROM shop_offers;
                DELETE FROM community_inventory_entries;
                DELETE FROM community_economies;
                DELETE FROM community_identities;
                """,
                cancellationToken: TestToken));
    }

    private static async Task<CommunityIdentityId> CreateIdentityAsync(
        PostgreSqlConnectionFactory factory)
    {
        var identityId = CommunityIdentityId.New();
        var repository = new CommunityIdentityRepository(factory);
        await repository.AddAsync(
            CommunityIdentity.Create(identityId),
            TestToken);
        return identityId;
    }

    private static async Task CreditAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId identityId,
        long amount)
    {
        _ = await new CommunityEconomyStore(factory)
            .CreditAsync(identityId, amount, TestToken);
    }

    private static async Task<ShopOffer> CreateEnabledOfferAsync(
        PostgreSqlConnectionFactory factory,
        ItemDefinitionId itemDefinitionId,
        long price,
        int? purchaseLimit = null)
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            itemDefinitionId,
            "Purchase-Test-Angebot",
            null,
            ShopPrice.Create(price),
            AvailabilityWindow.Create(null, null),
            purchaseLimit);
        offer.Enable();
        await new ShopOfferStore(factory).AddAsync(offer, TestToken);
        return offer;
    }

    private static async Task<PurchaseAttempt> CaptureAsync(Func<Task<ShopPurchase>> operation)
    {
        try
        {
            return new PurchaseAttempt(await operation(), null);
        }
        catch (Exception exception)
        {
            return new PurchaseAttempt(null, exception);
        }
    }

    private static Task<long> ReadBalanceAsync(
        System.Data.Common.DbConnection connection,
        CommunityIdentityId identityId) =>
        connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @IdentityId;",
                new { IdentityId = identityId.Value },
                cancellationToken: TestToken));

    private static Task<long> ReadQuantityAsync(
        System.Data.Common.DbConnection connection,
        CommunityIdentityId identityId,
        ItemDefinitionId itemDefinitionId) =>
        connection.QuerySingleAsync<long>(
            new CommandDefinition(
                """
                SELECT quantity
                FROM community_inventory_entries
                WHERE community_identity_id = @IdentityId
                  AND item_definition_id = @ItemDefinitionId;
                """,
                new
                {
                    IdentityId = identityId.Value,
                    ItemDefinitionId = itemDefinitionId.Value
                },
                cancellationToken: TestToken));

    private static Task<long?> ReadQuantityOrNullAsync(
        System.Data.Common.DbConnection connection,
        CommunityIdentityId identityId,
        ItemDefinitionId itemDefinitionId) =>
        connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                """
                SELECT quantity
                FROM community_inventory_entries
                WHERE community_identity_id = @IdentityId
                  AND item_definition_id = @ItemDefinitionId;
                """,
                new
                {
                    IdentityId = identityId.Value,
                    ItemDefinitionId = itemDefinitionId.Value
                },
                cancellationToken: TestToken));

    private static Task<long> CountAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        var allowedTableNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "shop_purchases",
            "shop_purchase_requests",
            "shop_purchase_guards"
        };

        if (!allowedTableNames.Contains(tableName))
        {
            throw new ArgumentOutOfRangeException(nameof(tableName));
        }

        return connection.QuerySingleAsync<long>(
            new CommandDefinition(
                $"SELECT COUNT(*)::bigint FROM {tableName};",
                cancellationToken: TestToken));
    }

    private static Task<long> CountOutboxAsync(System.Data.Common.DbConnection connection) =>
        connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*)::bigint FROM flurnetz_messaging.outbox_messages;",
                cancellationToken: TestToken));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed record PurchaseAttempt(ShopPurchase? Purchase, Exception? Error);

    private sealed class PurchaseRow
    {
        public Guid Id { get; set; }
        public Guid ShopOfferId { get; set; }
        public Guid CommunityIdentityId { get; set; }
        public Guid ItemDefinitionId { get; set; }
        public long PricePaid { get; set; }
        public DateTimeOffset PurchasedAt { get; set; }
    }

    private sealed class RequestRow
    {
        public Guid RequestId { get; set; }
        public Guid ShopPurchaseId { get; set; }
        public Guid ShopOfferId { get; set; }
        public Guid CommunityIdentityId { get; set; }
    }

    private sealed class OutboxRow
    {
        public string MessageType { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public DateTimeOffset OccurredAtUtc { get; set; }
        public string? CorrelationId { get; set; }
    }
}
