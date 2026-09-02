using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Processing;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Automation.Migrations;
using FlurNetz.Modules.Automation.Persistence;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Contracts;
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
/// Prüft Automation V1 über echte Outbox-, Inbox- und PostgreSQL-Transaktionen.
/// </summary>
public sealed class AutomationWorkflowIntegrationTests(WorkflowPostgreSqlFixture database)
    : IClassFixture<WorkflowPostgreSqlFixture>
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 9, 2, 18, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task EngagementEventMatchesRuleAndDuplicateDeliveryHasNoSecondEffect()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementDatabaseAsync(factory);

        var identityId = Guid.NewGuid();
        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(),
            "Engagement-Bonus",
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [AutomationCondition.Create(0, AutomationConditionTypes.CommunityIdentityEquals, communityIdentityId: identityId)],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 11)],
            0,
            Now);
        await ruleStore.AddAsync(rule, TestToken);
        _ = await ruleStore.MutateAsync(rule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            messageId,
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion,
            Now,
            new MessageEngagementRecordedIntegrationEvent(identityId));
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await publisher.EnqueueAsync(transaction, envelope, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        var processor = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(CreateEngine(factory))));
        var first = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, first.ProcessedCount);
        Assert.Equal(11, await ReadBalanceAsync(factory, identityId));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName AND message_id = @MessageId;",
            new { ConsumerName = EngagementMessageRecordedAutomationConsumer.ConsumerName, MessageId = messageId }));

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE flurnetz_messaging.outbox_messages SET status = 'pending', processed_at_utc = NULL, next_attempt_at_utc = @Now;",
                new { Now },
                cancellationToken: TestToken));
        }

        var duplicate = await processor.ProcessBatchAsync(TestToken);
        Assert.Equal(1, duplicate.DuplicateDeliveryCount);
        Assert.Equal(11, await ReadBalanceAsync(factory, identityId));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
    }

    [Fact]
    public async Task RealShopPurchaseRunsAutomationCreditAndNotificationAlongsideExistingConsumer()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopDatabaseAsync(factory);

        var identityId = CommunityIdentityId.New();
        await new CommunityIdentityRepository(factory).AddAsync(CommunityIdentity.Create(identityId), TestToken);
        _ = await new CommunityEconomyStore(factory).CreditAsync(identityId, 100, TestToken);

        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Automation-Angebot",
            null,
            ShopPrice.Create(25),
            AvailabilityWindow.Create(null, null));
        offer.Enable();
        await new ShopOfferStore(factory).AddAsync(offer, TestToken);

        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(),
            "Kauf-Bonus",
            null,
            AutomationTriggerTypes.ShopPurchaseCompleted,
            [AutomationCondition.Create(0, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 20)],
            [
                AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 7),
                AutomationAction.Create(1, AutomationActionTypes.NotificationCreate, title: "Bonus gutgeschrieben", message: "Danke für den Kauf.")
            ],
            0,
            Now);
        await ruleStore.AddAsync(rule, TestToken);
        _ = await ruleStore.MutateAsync(rule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        var purchase = await new PurchaseShopOffer(
            new PostgreSqlShopPurchaseExecutor(
                factory,
                new CommunityIdentityExistence(),
                new EconomyBalanceDebit(new CommunityEconomyStore(factory)),
                new InventoryQuantityGrant(new CommunityInventoryStore(factory)),
                publisher),
            new FixedClock(Now)).ExecuteAsync(
                ShopPurchaseRequestId.New(),
                offer.Id,
                identityId,
                TestToken);

        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [
                new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
                    "notifications.shop-purchase",
                    new ShopPurchaseCompletedIntegrationEventHandler(
                        new CreateNotification(new CommunityNotificationStore(factory), new FixedClock(Now)))),
                new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
                    ShopPurchaseCompletedAutomationConsumer.ConsumerName,
                    new ShopPurchaseCompletedAutomationConsumer(CreateEngine(factory)))
            ],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero },
            new FixedClock(Now));

        var processed = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, processed.ProcessedCount);
        Assert.Equal(82, await ReadBalanceAsync(factory, identityId.Value));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM automation_executions WHERE automation_rule_id = @RuleId AND trigger_message_id IN (SELECT message_id FROM flurnetz_messaging.outbox_messages);",
            new { RuleId = rule.Id.Value }));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName;",
            new { ConsumerName = ShopPurchaseCompletedAutomationConsumer.ConsumerName }));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName;",
            new { ConsumerName = "notifications.shop-purchase" }));
        Assert.Equal(2, await CountAsync(factory, "SELECT COUNT(*) FROM community_notifications;"));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM community_notifications WHERE source_type = 'automation.execution' AND source_id IS NOT NULL;"));
        Assert.Equal(purchase.CommunityIdentityId, identityId);
    }

    [Fact]
    public async Task EngagementEventWithoutMatchingRuleCreatesNoExecution()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementDatabaseAsync(factory);

        var identityId = Guid.NewGuid();
        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), "No-Match", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [AutomationCondition.Create(0, AutomationConditionTypes.CommunityIdentityEquals, communityIdentityId: Guid.NewGuid())],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 11)], 0, Now);
        await ruleStore.AddAsync(rule, TestToken);
        _ = await ruleStore.MutateAsync(rule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var messageId = Guid.NewGuid();
        await EnqueueAsync(factory, publisher, CreateEngagementEnvelope(messageId, identityId));
        var processor = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(CreateEngine(factory))));

        var result = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_economies;"));
    }

    [Fact]
    public async Task DisabledAndArchivedRulesAreIgnoredByEngagementConsumer()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementDatabaseAsync(factory);

        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var disabledRule = AutomationRule.Create(
            AutomationRuleId.New(), "Disabled", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 3)], 0, Now);
        var archivedRule = AutomationRule.Create(
            AutomationRuleId.New(), "Archived", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 5)], 1, Now);
        await ruleStore.AddAsync(disabledRule, TestToken);
        await ruleStore.AddAsync(archivedRule, TestToken);
        _ = await ruleStore.MutateAsync(archivedRule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);
        _ = await ruleStore.MutateAsync(archivedRule.Id, value => value.Archive(Now.AddMinutes(2)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        await EnqueueAsync(factory, publisher, CreateEngagementEnvelope(Guid.NewGuid(), Guid.NewGuid()));
        var processor = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(CreateEngine(factory))));

        var result = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_economies;"));
    }

    [Fact]
    public async Task FailedLaterActionRollsBackPriorActionExecutionAndInboxAndRetrySucceeds()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementDatabaseAsync(factory);

        var identityId = Guid.NewGuid();
        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(),
            "Rollback-Regel",
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [
                AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 13),
                AutomationAction.Create(1, AutomationActionTypes.NotificationCreate, title: "Nachricht")
            ],
            0,
            Now);
        await ruleStore.AddAsync(rule, TestToken);
        _ = await ruleStore.MutateAsync(rule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            messageId,
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion,
            Now,
            new MessageEngagementRecordedIntegrationEvent(identityId));
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await publisher.EnqueueAsync(transaction, envelope, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        var failingProcessor = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(
                    CreateEngine(factory, new FailingNotificationCreateCapability()))));
        var failed = await failingProcessor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, failed.RetriedCount);
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_economies;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_notifications;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(0, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName AND message_id = @MessageId;",
            new { ConsumerName = EngagementMessageRecordedAutomationConsumer.ConsumerName, MessageId = messageId }));

        var retry = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(CreateEngine(factory))));
        var succeeded = await retry.ProcessBatchAsync(TestToken);

        Assert.Equal(1, succeeded.ProcessedCount);
        Assert.Equal(13, await ReadBalanceAsync(factory, identityId));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM community_notifications;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName AND message_id = @MessageId;",
            new { ConsumerName = EngagementMessageRecordedAutomationConsumer.ConsumerName, MessageId = messageId }));
    }

    [Fact]
    public async Task FailedLaterRuleRollsBackEarlierRuleEffectsAndRetrySucceeds()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementDatabaseAsync(factory);

        var identityId = Guid.NewGuid();
        var ruleStore = new PostgreSqlAutomationRuleStore(factory);
        var creditRule = AutomationRule.Create(
            AutomationRuleId.New(), "First", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 17)], 0, Now);
        var notificationRule = AutomationRule.Create(
            AutomationRuleId.New(), "Second", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Second")], 1, Now);
        await ruleStore.AddAsync(creditRule, TestToken);
        await ruleStore.AddAsync(notificationRule, TestToken);
        _ = await ruleStore.MutateAsync(creditRule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);
        _ = await ruleStore.MutateAsync(notificationRule.Id, value => value.Enable(Now.AddMinutes(1)), TestToken);

        var (publisher, serializer, registry) = CreatePublisher<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var messageId = Guid.NewGuid();
        await EnqueueAsync(factory, publisher, CreateEngagementEnvelope(messageId, identityId));
        var failingProcessor = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(
                    CreateEngine(factory, new FailingNotificationCreateCapability()))));

        var failed = await failingProcessor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, failed.RetriedCount);
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_economies;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM community_notifications;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));

        var retry = CreateAutomationProcessor(
            factory,
            serializer,
            registry,
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                new EngagementMessageRecordedAutomationConsumer(CreateEngine(factory))));
        var succeeded = await retry.ProcessBatchAsync(TestToken);

        Assert.Equal(1, succeeded.ProcessedCount);
        Assert.Equal(17, await ReadBalanceAsync(factory, identityId));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM community_notifications;"));
        Assert.Equal(2, await CountAsync(factory, "SELECT COUNT(*) FROM automation_executions;"));
        Assert.Equal(1, await CountAsync(factory,
            "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages WHERE consumer_name = @ConsumerName AND message_id = @MessageId;",
            new { ConsumerName = EngagementMessageRecordedAutomationConsumer.ConsumerName, MessageId = messageId }));
    }

    private ExecuteAutomationTrigger CreateEngine(
        PostgreSqlConnectionFactory factory,
        ICommunityNotificationCreate? notificationCreate = null) =>
        new(
            new PostgreSqlAutomationRuntimeStore(),
            new EconomyBalanceCredit(new CommunityEconomyStore(factory)),
            notificationCreate ?? new CommunityNotificationCreateCapability(
                new CreateNotification(new CommunityNotificationStore(factory), new FixedClock(Now))),
            new FixedClock(Now));

    private static OutboxProcessor CreateAutomationProcessor(
        PostgreSqlConnectionFactory factory,
        IntegrationEventJsonSerializer serializer,
        IntegrationEventTypeRegistry registry,
        IIntegrationEventHandlerRegistration registration) =>
        new(
            factory,
            serializer,
            registry,
            [registration],
            new OutboxProcessingOptions { RetryDelay = TimeSpan.Zero },
            new FixedClock(Now));

    private async Task PrepareEngagementDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetDatabaseAsync(factory);
        await new MigrationRunner(factory, [
            new MessagingMigrationSource(),
            new EconomyMigrationSource(),
            new NotificationsMigrationSource(),
            new AutomationMigrationSource()
        ]).RunAsync(TestToken);
    }

    private async Task PrepareShopDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetDatabaseAsync(factory);
        await new MigrationRunner(factory, [
            new MessagingMigrationSource(),
            new IdentityMigrationSource(),
            new EconomyMigrationSource(),
            new InventoryMigrationSource(),
            new ShopMigrationSource(),
            new NotificationsMigrationSource(),
            new AutomationMigrationSource()
        ]).RunAsync(TestToken);
    }

    private async Task ResetDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DROP SCHEMA IF EXISTS flurnetz_messaging CASCADE;
            DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;
            DROP TABLE IF EXISTS community_notifications CASCADE;
            DROP TABLE IF EXISTS automation_executions CASCADE;
            DROP TABLE IF EXISTS automation_rule_actions CASCADE;
            DROP TABLE IF EXISTS automation_rule_conditions CASCADE;
            DROP TABLE IF EXISTS automation_rules CASCADE;
            DROP TABLE IF EXISTS shop_purchase_requests CASCADE;
            DROP TABLE IF EXISTS shop_purchase_guards CASCADE;
            DROP TABLE IF EXISTS shop_purchases CASCADE;
            DROP TABLE IF EXISTS shop_offers CASCADE;
            DROP TABLE IF EXISTS community_inventory_entries CASCADE;
            DROP TABLE IF EXISTS community_economies CASCADE;
            DROP TABLE IF EXISTS community_identities CASCADE;
            """,
            cancellationToken: TestToken));
    }

    private static (PostgreSqlOutboxPublisher Publisher, IntegrationEventJsonSerializer Serializer, IntegrationEventTypeRegistry Registry)
        CreatePublisher<TEvent>(string messageType, int schemaVersion)
        where TEvent : IIntegrationEvent
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<TEvent>(messageType, schemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        return (new PostgreSqlOutboxPublisher(serializer, new FixedClock(Now)), serializer, registry);
    }

    private static async Task<long> ReadBalanceAsync(PostgreSqlConnectionFactory factory, Guid identityId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @IdentityId;",
                new { IdentityId = identityId },
                cancellationToken: TestToken));
    }

    private static IntegrationEventEnvelope CreateEngagementEnvelope(Guid messageId, Guid identityId) =>
        new(
            messageId,
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion,
            Now,
            new MessageEngagementRecordedIntegrationEvent(identityId));

    private static async Task EnqueueAsync(
        PostgreSqlConnectionFactory factory,
        PostgreSqlOutboxPublisher publisher,
        IntegrationEventEnvelope envelope)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken);
        await publisher.EnqueueAsync(transaction, envelope, TestToken);
        await transaction.CommitAsync(TestToken);
    }

    private static async Task<int> CountAsync(PostgreSqlConnectionFactory factory, string sql, object? parameters = null)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FailingNotificationCreateCapability : ICommunityNotificationCreate
    {
        public Task CreateAsync(
            CommunityIdentityId communityIdentityId,
            string notificationType,
            string title,
            string? message,
            string sourceType,
            string sourceId,
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Absichtlicher Notification-Fehler."));
    }
}
