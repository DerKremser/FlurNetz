using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Processing;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Migrations;
using FlurNetz.Modules.Notifications.Persistence;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Workflows.IntegrationTests;

/// <summary>
/// Prüft den ersten Shop-Purchase-Event-zu-Notification-Workflow mit echter Outbox und Inbox.
/// </summary>
public sealed class ShopPurchaseNotificationsWorkflowIntegrationTests(WorkflowPostgreSqlFixture database)
    : IClassFixture<WorkflowPostgreSqlFixture>
{
    private static readonly DateTimeOffset TestNow =
        new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task ShopPurchaseCompletedOutboxMessageCreatesExactlyOneNotification()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = new PostgreSqlConnectionFactory(new PostgreSqlOptions(database.ConnectionString));
        await PrepareDatabaseAsync(factory);

        var registry = new IntegrationEventTypeRegistry();
        registry.Register<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        var clock = new FixedClock(TestNow);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var identityId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion,
            TestNow,
            new ShopPurchaseCompletedIntegrationEvent(
                purchaseId,
                Guid.NewGuid(),
                identityId,
                Guid.NewGuid(),
                25,
                TestNow));

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await publisher.EnqueueAsync(transaction, envelope, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        var handler = new ShopPurchaseCompletedIntegrationEventHandler(
            new CreateNotification(new CommunityNotificationStore(factory), clock));
        var registration = new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
            handler);
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [registration],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero },
            clock);

        var result = await processor.ProcessBatchAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var notification = await connection.QuerySingleAsync<NotificationRow>(
            new CommandDefinition(
                "SELECT id AS Id, community_identity_id AS CommunityIdentityId, notification_type AS NotificationType, source_type AS SourceType, source_id AS SourceId FROM community_notifications;",
                cancellationToken: TestToken));

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(identityId, notification.CommunityIdentityId);
        Assert.Equal("shop.purchase-completed", notification.NotificationType);
        Assert.Equal("shop.purchase", notification.SourceType);
        Assert.Equal(purchaseId.ToString("D"), notification.SourceId);
        Assert.Equal(1L, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName AND message_id = @MessageId;",
                new
                {
                    ConsumerName = ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
                    MessageId = envelope.MessageId
                },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task RealShopPurchaseFlowsThroughOutboxProcessorToNotificationInbox()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = new PostgreSqlConnectionFactory(new PostgreSqlOptions(database.ConnectionString));
        await PrepareShopNotificationDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        await new CommunityIdentityRepository(factory)
            .AddAsync(CommunityIdentity.Create(identityId), TestToken);
        _ = await new CommunityEconomyStore(factory).CreditAsync(identityId, 100, TestToken);

        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Workflow-Angebot",
            null,
            ShopPrice.Create(25),
            AvailabilityWindow.Create(null, null));
        offer.Enable();
        await new ShopOfferStore(factory).AddAsync(offer, TestToken);

        var clock = new FixedClock(TestNow);
        var (publisher, serializer, registry) = CreatePublisher();
        var purchase = new PurchaseShopOffer(
            new PostgreSqlShopPurchaseExecutor(
                factory,
                new CommunityIdentityExistence(),
                new EconomyBalanceDebit(new CommunityEconomyStore(factory)),
                new InventoryQuantityGrant(new CommunityInventoryStore(factory)),
                publisher),
            clock);

        var shopPurchase = await purchase.ExecuteAsync(
            ShopPurchaseRequestId.New(),
            offer.Id,
            identityId,
            TestToken);
        var pendingOutbox = await ReadSingleOutboxMessageAsync(factory);

        Assert.Equal(shopPurchase.Id.Value, ParsePurchaseId(pendingOutbox.Payload));
        Assert.Equal("pending", pendingOutbox.Status);
        Assert.Equal(ShopPurchaseCompletedIntegrationEvent.MessageType, pendingOutbox.MessageType);

        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
                new ShopPurchaseCompletedIntegrationEventHandler(
                    new CreateNotification(new CommunityNotificationStore(factory), clock)))],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero },
            clock);

        var processing = await processor.ProcessBatchAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var notification = await connection.QuerySingleAsync<NotificationRow>(
            new CommandDefinition(
                "SELECT id AS Id, community_identity_id AS CommunityIdentityId, notification_type AS NotificationType, source_type AS SourceType, source_id AS SourceId FROM community_notifications;",
                cancellationToken: TestToken));

        Assert.Equal(1, processing.ProcessedCount);
        Assert.Equal(identityId.Value, notification.CommunityIdentityId);
        Assert.Equal("shop.purchase-completed", notification.NotificationType);
        Assert.Equal("shop.purchase", notification.SourceType);
        Assert.Equal(shopPurchase.Id.Value.ToString("D"), notification.SourceId);
        Assert.Equal(1L, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName;",
                new { ConsumerName = ShopPurchaseCompletedIntegrationEventHandler.ConsumerName },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DuplicateDeliveryDoesNotCreateSecondNotification()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = new PostgreSqlConnectionFactory(new PostgreSqlOptions(database.ConnectionString));
        await PrepareDatabaseAsync(factory);

        var (publisher, serializer, registry) = CreatePublisher();
        var eventPayload = new ShopPurchaseCompletedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, TestNow);
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion,
            TestNow,
            eventPayload);
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await publisher.EnqueueAsync(transaction, envelope, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        var clock = new FixedClock(TestNow);
        var registration = new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
            new ShopPurchaseCompletedIntegrationEventHandler(
                new CreateNotification(new CommunityNotificationStore(factory), clock)));
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [registration],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero },
            clock);

        Assert.Equal(1, (await processor.ProcessBatchAsync(TestToken)).ProcessedCount);
        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE flurnetz_messaging.outbox_messages SET status = 'pending', processed_at_utc = NULL, next_attempt_at_utc = @NowUtc;",
                new { NowUtc = TestNow },
                cancellationToken: TestToken));
        }

        var duplicate = await processor.ProcessBatchAsync(TestToken);
        await using var verification = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, duplicate.DuplicateDeliveryCount);
        Assert.Equal(1L, await verification.QuerySingleAsync<long>(
            new CommandDefinition("SELECT COUNT(*) FROM community_notifications;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task ConsumerFailureRollsBackNotificationAndInboxAndRetrySucceeds()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = new PostgreSqlConnectionFactory(new PostgreSqlOptions(database.ConnectionString));
        await PrepareDatabaseAsync(factory);

        var (publisher, serializer, registry) = CreatePublisher();
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion,
            TestNow,
            new ShopPurchaseCompletedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, TestNow));
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await publisher.EnqueueAsync(transaction, envelope, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DROP TABLE community_notifications; DELETE FROM flurnetz_persistence.migration_history WHERE owner = 'Notifications' AND version = 1;",
                cancellationToken: TestToken));
        }

        var clock = new FixedClock(TestNow);
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
                new ShopPurchaseCompletedIntegrationEventHandler(
                    new CreateNotification(new CommunityNotificationStore(factory), clock)))],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero, MaxAttempts = 2 },
            clock);

        var failed = await processor.ProcessBatchAsync(TestToken);
        await using (var verification = await factory.OpenConnectionAsync(TestToken))
        {
            Assert.Equal(1, failed.RetriedCount);
            Assert.Equal(0, failed.ProcessedCount);
            Assert.Equal(0L, await verification.QuerySingleAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE message_id = @MessageId;",
                    new { MessageId = envelope.MessageId },
                    cancellationToken: TestToken)));
        }

        await new MigrationRunner(factory, new NotificationsMigrationSource()).RunAsync(TestToken);
        var retried = await processor.ProcessBatchAsync(TestToken);

        await using var finalVerification = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, retried.ProcessedCount);
        Assert.Equal(1L, await finalVerification.QuerySingleAsync<long>(
            new CommandDefinition("SELECT COUNT(*) FROM community_notifications;", cancellationToken: TestToken)));
        Assert.Equal(1L, await finalVerification.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE message_id = @MessageId;",
                new { MessageId = envelope.MessageId },
                cancellationToken: TestToken)));
    }

    private async Task PrepareDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DROP TABLE IF EXISTS community_notifications, shop_purchase_guards, shop_purchase_requests, shop_purchases, shop_offers, community_inventory_entries, community_economies, community_identities CASCADE; DROP SCHEMA IF EXISTS flurnetz_messaging CASCADE; DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;",
            cancellationToken: TestToken));
        await new MigrationRunner(factory, [new MessagingMigrationSource(), new NotificationsMigrationSource()])
            .RunAsync(TestToken);
    }

    private async Task PrepareShopNotificationDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DROP TABLE IF EXISTS community_notifications, shop_purchase_guards, shop_purchase_requests, shop_purchases, shop_offers, community_inventory_entries, community_economies, community_identities CASCADE; DROP SCHEMA IF EXISTS flurnetz_messaging CASCADE; DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;",
            cancellationToken: TestToken));
        await new MigrationRunner(
                factory,
                [
                    new MessagingMigrationSource(),
                    new IdentityMigrationSource(),
                    new EconomyMigrationSource(),
                    new InventoryMigrationSource(),
                    new ShopMigrationSource(),
                    new NotificationsMigrationSource()
                ])
            .RunAsync(TestToken);
    }

    private static (PostgreSqlOutboxPublisher Publisher, IntegrationEventJsonSerializer Serializer, IntegrationEventTypeRegistry Registry) CreatePublisher()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        return (new PostgreSqlOutboxPublisher(serializer, new FixedClock(TestNow)), serializer, registry);
    }

    private static async Task<OutboxSnapshot> ReadSingleOutboxMessageAsync(
        PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<OutboxSnapshot>(
            new CommandDefinition(
                "SELECT payload::text AS Payload, message_type AS MessageType, status AS Status FROM flurnetz_messaging.outbox_messages;",
                cancellationToken: TestToken));
    }

    private static Guid ParsePurchaseId(string payload)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("shopPurchaseId").GetGuid();
    }

    private void SkipIfDatabaseIsUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class NotificationRow
    {
        public Guid Id { get; set; }
        public Guid CommunityIdentityId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
    }

    private sealed class OutboxSnapshot
    {
        public string Payload { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
