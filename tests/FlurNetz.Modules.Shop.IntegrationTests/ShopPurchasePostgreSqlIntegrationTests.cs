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
using FlurNetz.Persistence.Transactions;
using Npgsql;

namespace FlurNetz.Modules.Shop.IntegrationTests;

/// <summary>
/// Prüft den ersten Shop-Purchase-Slice Ende zu Ende innerhalb einer echten PostgreSQL-Transaktion.
/// </summary>
public sealed class ShopPurchasePostgreSqlIntegrationTests(ShopPostgreSqlFixture database)
    : IClassFixture<ShopPostgreSqlFixture>
{
    private static readonly DateTimeOffset PurchaseTime =
        new(2026, 8, 31, 16, 15, 0, TimeSpan.Zero);

    private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromSeconds(10);

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
            purchaseLimit: 2,
            sortOrder: 5000);
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
        var purchaseSortOrderColumnCount = await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::bigint
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'shop_purchases'
                  AND column_name = 'sort_order';
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
        Assert.Equal(0L, purchaseSortOrderColumnCount);
    }

    [Fact]
    public async Task FreePurchaseSkipsEconomyRowAndCommitsInventoryPurchaseAndOutbox()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 0);
        var useCase = CreateUseCase(factory, PurchaseTime);

        var purchase = await useCase.ExecuteAsync(
            ShopPurchaseRequestId.New(),
            offer.Id,
            identityId,
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Null(await ReadBalanceOrNullAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(0, purchase.PricePaid.Value);
        Assert.Equal(1L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task InventoryOverflowRollsBackPriorEconomyDebitAndAllPurchaseWrites()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        _ = await new CommunityInventoryStore(factory)
            .AddAsync(identityId, itemDefinitionId, long.MaxValue, TestToken);
        var offer = await CreateEnabledOfferAsync(
            factory,
            itemDefinitionId,
            price: 25,
            purchaseLimit: 1);
        var useCase = CreateUseCase(factory, PurchaseTime);

        await Assert.ThrowsAsync<OverflowException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(100, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(long.MaxValue, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task OutboxFailureRollsBackDebitInventoryPurchaseRequestAndGuard()
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
            purchaseLimit: 1);
        var useCase = CreateUseCase(
            factory,
            PurchaseTime,
            new ThrowingIntegrationEventPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(100, await ReadBalanceAsync(connection, identityId));
        Assert.Null(await ReadQuantityOrNullAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task CorruptIdempotencyMappingFailsVisiblyWithoutReapplyingEffects()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 25);
        var requestId = ShopPurchaseRequestId.New();

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO shop_purchase_requests
                        (request_id, shop_purchase_id, shop_offer_id, community_identity_id)
                    VALUES
                        (@RequestId, @ShopPurchaseId, @ShopOfferId, @CommunityIdentityId);
                    """,
                    new
                    {
                        RequestId = requestId.Value,
                        ShopPurchaseId = Guid.NewGuid(),
                        ShopOfferId = offer.Id.Value,
                        CommunityIdentityId = identityId.Value
                    },
                    cancellationToken: TestToken));
        }

        var useCase = CreateUseCase(factory, PurchaseTime);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(
                requestId,
                offer.Id,
                identityId,
                TestToken));

        Assert.Contains("keinen persistierten Kauf", exception.Message, StringComparison.Ordinal);

        await using var verificationConnection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(100, await ReadBalanceAsync(verificationConnection, identityId));
        Assert.Null(await ReadQuantityOrNullAsync(verificationConnection, identityId, itemDefinitionId));
        Assert.Equal(0L, await CountAsync(verificationConnection, "shop_purchases"));
        Assert.Equal(1L, await CountAsync(verificationConnection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountOutboxAsync(verificationConnection));
    }

    [Fact]
    public async Task PurchaseSnapshotRemainsUnchangedAfterLaterOfferPriceMutation()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 25);
        var useCase = CreateUseCase(factory, PurchaseTime);

        var purchase = await useCase.ExecuteAsync(
            ShopPurchaseRequestId.New(),
            offer.Id,
            identityId,
            TestToken);

        Assert.True(await new ShopOfferStore(factory).ExecuteAsync(
            offer.Id,
            current => current.ChangePrice(99),
            TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<PurchaseRow>(
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

        Assert.Equal(25, row.PricePaid);
        Assert.Equal(itemDefinitionId.Value, row.ItemDefinitionId);
        Assert.Equal(PurchaseTime, row.PurchasedAt);
        Assert.Equal(99, (await new ShopOfferStore(factory).GetAsync(offer.Id, TestToken))!.Price.Value);
    }

    [Fact]
    public async Task UnknownIdentityRollsBackRequestWithoutAnyPurchaseEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 0);
        var unknownIdentityId = CommunityIdentityId.New();
        var useCase = CreateUseCase(factory, PurchaseTime);

        var exception = await Assert.ThrowsAsync<ShopPurchaseIdentityNotFoundException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                unknownIdentityId,
                TestToken));

        Assert.Equal(unknownIdentityId, exception.CommunityIdentityId);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task UnknownOfferRollsBackRequestWithoutAnyPurchaseEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        var useCase = CreateUseCase(factory, PurchaseTime);

        await Assert.ThrowsAsync<ShopOfferNotFoundException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                ShopOfferId.New(),
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task DisabledOfferIsRejectedAndRequestReservationRollsBack()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Deaktiviertes Angebot",
            null,
            ShopPrice.Zero,
            AvailabilityWindow.Create(null, null));
        await new ShopOfferStore(factory).AddAsync(offer, TestToken);
        var useCase = CreateUseCase(factory, PurchaseTime);

        await Assert.ThrowsAsync<ShopOfferUnavailableForPurchaseException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task OfferOutsideAvailabilityWindowIsRejectedAndRequestReservationRollsBack()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Zukünftiges Angebot",
            null,
            ShopPrice.Zero,
            AvailabilityWindow.Create(PurchaseTime.AddHours(1), null));
        offer.Enable();
        await new ShopOfferStore(factory).AddAsync(offer, TestToken);
        var useCase = CreateUseCase(factory, PurchaseTime);

        await Assert.ThrowsAsync<ShopOfferUnavailableForPurchaseException>(
            () => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task ReusingRequestForDifferentIdentityIsRejectedWithoutSecondEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var firstIdentityId = await CreateIdentityAsync(factory);
        var secondIdentityId = await CreateIdentityAsync(factory);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 0);
        var requestId = ShopPurchaseRequestId.New();
        var useCase = CreateUseCase(factory, PurchaseTime);

        var firstPurchase = await useCase.ExecuteAsync(
            requestId,
            offer.Id,
            firstIdentityId,
            TestToken);

        var exception = await Assert.ThrowsAsync<ShopPurchaseIdempotencyConflictException>(
            () => useCase.ExecuteAsync(
                requestId,
                offer.Id,
                secondIdentityId,
                TestToken));

        Assert.Equal(requestId, exception.RequestId);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, await ReadQuantityAsync(connection, firstIdentityId, itemDefinitionId));
        Assert.Null(await ReadQuantityOrNullAsync(connection, secondIdentityId, itemDefinitionId));
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
    public async Task UnlimitedParallelPurchasesBothSucceedWithoutPurchaseGuard()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PreparePurchaseDatabaseAsync(factory);

        var identityId = await CreateIdentityAsync(factory);
        await CreditAsync(factory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(factory, itemDefinitionId, price: 10);
        var useCase = CreateUseCase(factory, PurchaseTime);

        var purchases = await Task.WhenAll(
            useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken),
            useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

        Assert.NotEqual(purchases[0].Id, purchases[1].Id);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(80, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(2, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(2L, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(2L, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0L, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(2L, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task CatalogMutationWaitsForPurchaseOfferSnapshotLock()
    {
        SkipIfDatabaseIsUnavailable();
        await using var setupFactory = CreateFactory();
        await PreparePurchaseDatabaseAsync(setupFactory);

        var identityId = await CreateIdentityAsync(setupFactory);
        await CreditAsync(setupFactory, identityId, 100);
        var itemDefinitionId = ItemDefinitionId.New();
        var offer = await CreateEnabledOfferAsync(setupFactory, itemDefinitionId, price: 25);

        var purchaseApplicationName = $"shop-purchase-lock-{offer.Id.Value:N}";
        var mutationApplicationName = $"shop-mutation-lock-{offer.Id.Value:N}";
        await using var purchaseFactory = CreateFactory(purchaseApplicationName);
        await using var mutationFactory = CreateFactory(mutationApplicationName);
        await using var observerFactory = CreateFactory();

        var blockingPublisher = new BlockingIntegrationEventPublisher(
            CreateOutboxPublisher(PurchaseTime));
        var useCase = CreateUseCase(purchaseFactory, PurchaseTime, blockingPublisher);

        Task<ShopPurchase>? purchaseTask = null;
        Task<bool>? mutationTask = null;

        try
        {
            purchaseTask = Task.Run(() => useCase.ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken));

            await blockingPublisher.Entered.Task.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken);

            mutationTask = Task.Run(() => new ShopOfferStore(mutationFactory).ExecuteAsync(
                offer.Id,
                current => current.ChangePrice(99),
                TestToken));

            await WaitForRowLockWaitAsync(observerFactory, mutationApplicationName);
            Assert.False(mutationTask.IsCompleted);

            blockingPublisher.Release.TrySetResult(true);

            var purchase = await purchaseTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken);
            Assert.True(await mutationTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken));

            Assert.Equal(25, purchase.PricePaid.Value);
            Assert.Equal(99, (await new ShopOfferStore(setupFactory).GetAsync(offer.Id, TestToken))!.Price.Value);

            await using var connection = await setupFactory.OpenConnectionAsync(TestToken);
            Assert.Equal(1L, await CountAsync(connection, "shop_purchases"));
            Assert.Equal(1L, await CountOutboxAsync(connection));
        }
        finally
        {
            blockingPublisher.Release.TrySetResult(true);
            await DrainTaskAsync(purchaseTask);
            await DrainTaskAsync(mutationTask);
        }
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
        Assert.Single(attempts, attempt => attempt.Error is ShopPurchaseLimitExceededException);

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

    private PostgreSqlConnectionFactory CreateFactory(string? applicationName = null)
    {
        var connectionString = database.ConnectionString;

        if (applicationName is not null)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName
            };
            connectionString = builder.ConnectionString;
        }

        return new PostgreSqlConnectionFactory(new PostgreSqlOptions(connectionString));
    }

    private static PurchaseShopOffer CreateUseCase(
        PostgreSqlConnectionFactory factory,
        DateTimeOffset now,
        IIntegrationEventPublisher? publisher = null)
    {
        var clock = new FixedClock(now);
        publisher ??= CreateOutboxPublisher(now);

        var executor = new PostgreSqlShopPurchaseExecutor(
            factory,
            new CommunityIdentityExistence(),
            new EconomyBalanceDebit(new CommunityEconomyStore(factory)),
            new InventoryQuantityGrant(new CommunityInventoryStore(factory)),
            publisher);

        return new PurchaseShopOffer(executor, clock);
    }

    private static IIntegrationEventPublisher CreateOutboxPublisher(DateTimeOffset now)
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);

        return new PostgreSqlOutboxPublisher(
            new IntegrationEventJsonSerializer(registry),
            new FixedClock(now));
    }

    private static async Task PreparePurchaseDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        IMigrationSource[] migrationSources =
        [
            new MessagingMigrationSource(),
            new IdentityMigrationSource(),
            new EconomyMigrationSource(),
            new InventoryMigrationSource(),
            new ShopMigrationSource()
        ];

        await new MigrationRunner(factory, migrationSources)
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
        int? purchaseLimit = null,
        int sortOrder = 0)
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            itemDefinitionId,
            "Purchase-Test-Angebot",
            null,
            ShopPrice.Create(price),
            AvailabilityWindow.Create(null, null),
            purchaseLimit,
            sortOrder);
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

    private static Task<long?> ReadBalanceOrNullAsync(
        System.Data.Common.DbConnection connection,
        CommunityIdentityId identityId) =>
        connection.QuerySingleOrDefaultAsync<long?>(
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

    private static async Task WaitForRowLockWaitAsync(
        PostgreSqlConnectionFactory observerFactory,
        string applicationName)
    {
        await using var connection = await observerFactory.OpenConnectionAsync(TestToken);
        var deadline = DateTime.UtcNow + ConcurrencyTimeout;

        while (DateTime.UtcNow < deadline)
        {
            var isWaitingForLock = await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    """
                    SELECT EXISTS
                    (
                        SELECT 1
                        FROM pg_stat_activity
                        WHERE application_name = @ApplicationName
                          AND backend_type = 'client backend'
                          AND state = 'active'
                          AND wait_event_type = 'Lock'
                    );
                    """,
                    new { ApplicationName = applicationName },
                    cancellationToken: TestToken));

            if (isWaitingForLock)
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Die Verbindung {applicationName} wartete nicht auf einen PostgreSQL-Lock.");
    }

    private static async Task DrainTaskAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Der eigentliche Testpfad bewertet die erwartete Ausnahme.
        }
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class BlockingIntegrationEventPublisher(IIntegrationEventPublisher inner)
        : IIntegrationEventPublisher
    {
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task EnqueueAsync(
            PostgreSqlTransaction transaction,
            IntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            await inner.EnqueueAsync(transaction, envelope, cancellationToken);
        }
    }

    private sealed class ThrowingIntegrationEventPublisher : IIntegrationEventPublisher
    {
        public Task EnqueueAsync(
            PostgreSqlTransaction transaction,
            IntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Absichtlicher Outbox-Fehler für den Rollback-Test.");
        }
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
