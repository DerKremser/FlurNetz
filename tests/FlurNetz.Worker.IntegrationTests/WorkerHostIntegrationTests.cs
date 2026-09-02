using System.Diagnostics;
using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Shop.Contracts;
using NotificationsShopPurchaseHandler = FlurNetz.Modules.Notifications.Application.ShopPurchaseCompletedIntegrationEventHandler;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using FlurNetz.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlurNetz.Worker.IntegrationTests;

/// <summary>
/// Prüft die echte Generic-Host-Komposition und den dauerhaften PostgreSQL-Outbox-Loop.
/// </summary>
public sealed class WorkerHostIntegrationTests(WorkerPostgreSqlFixture database)
    : IClassFixture<WorkerPostgreSqlFixture>
{
    [Fact]
    public async Task StartupRunsMessagingProgressionNotificationsAutomationOverlayAndEconomyMigrationsAndEmptyQueueStaysHealthy()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        var host = CreateHost();
        try
        {
            using var timeout = CreateTimeout();

            await host.StartAsync(timeout.Token);

            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'flurnetz_messaging' AND table_name = 'outbox_messages';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'flurnetz_messaging' AND table_name = 'inbox_messages';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'community_progressions';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'community_notifications';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'automation_rules';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'automation_executions';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'overlay_channels';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'overlay_alerts';
                """,
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'community_economies';
                """,
                timeout.Token));
            Assert.Equal(0, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'engagement_activities';
                """,
                timeout.Token));
            Assert.Equal(0, await CountAsync(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name IN (
                      'shop_offers',
                      'shop_purchases',
                      'shop_purchase_requests',
                      'shop_purchase_guards');
                """,
                timeout.Token));
            Assert.Equal(
                0,
                await CountAsync(
                    "SELECT COUNT(*) FROM community_progressions;",
                    timeout.Token));

            await StopHostAsync(host, timeout.Token);
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    [Fact]
    public async Task MessageEnqueuedAfterStartupIsProcessedContinuouslyToExactlyOneExperiencePoint()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        var host = CreateHost();
        try
        {
            using var timeout = CreateTimeout();
            await host.StartAsync(timeout.Token);

            var communityIdentityId = Guid.NewGuid();
            var firstMessageId = await EnqueueMessageAsync(communityIdentityId, timeout.Token);
            await WaitForExperiencePointsAsync(communityIdentityId, 1, timeout.Token);

            Assert.Equal("processed", await ReadOutboxStatusAsync(firstMessageId, timeout.Token));
            Assert.Equal(
                1,
                await CountAsync(
                    """
                    SELECT COUNT(*)
                    FROM flurnetz_messaging.inbox_messages
                    WHERE consumer_name = @ConsumerName AND message_id = @MessageId;
                    """,
                    new
                    {
                        ConsumerName = MessageEngagementRecordedIntegrationEventHandler.ConsumerName,
                        MessageId = firstMessageId
                    },
                    timeout.Token));

            var secondMessageId = await EnqueueMessageAsync(communityIdentityId, timeout.Token);
            await WaitForExperiencePointsAsync(communityIdentityId, 2, timeout.Token);

            Assert.Equal("processed", await ReadOutboxStatusAsync(secondMessageId, timeout.Token));
            Assert.Equal(
                2,
                await CountAsync(
                    """
                    SELECT COUNT(*)
                    FROM flurnetz_messaging.inbox_messages
                    WHERE consumer_name = @ConsumerName;
                    """,
                    new { ConsumerName = MessageEngagementRecordedIntegrationEventHandler.ConsumerName },
                    timeout.Token));

            await StopHostAsync(host, timeout.Token);
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    [Fact]
    public async Task ShopPurchaseCompletedMessageCreatesOneNotificationAndInboxEntry()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        var host = CreateHost();
        try
        {
            using var timeout = CreateTimeout();
            await host.StartAsync(timeout.Token);

            var messageId = await EnqueueShopPurchaseCompletedMessageAsync(timeout.Token);
            await WaitForOutboxProcessedAsync(messageId, timeout.Token);

            var status = await ReadOutboxStatusAsync(messageId, timeout.Token);
            Assert.Equal("processed", status);
            Assert.NotEqual("pending", status);
            Assert.NotEqual("failed", status);
            Assert.Equal(1, await ReadOutboxAttemptCountAsync(messageId, timeout.Token));
            Assert.Equal(
                1,
                await CountAsync(
                    """
                    SELECT COUNT(*)
                    FROM flurnetz_messaging.inbox_messages
                    WHERE message_id = @MessageId
                      AND consumer_name = 'notifications.shop-purchase';
                    """,
                    new { MessageId = messageId },
                    timeout.Token));
            Assert.Equal(1, await CountAsync(
                "SELECT COUNT(*) FROM community_notifications;",
                timeout.Token));

            await StopHostAsync(host, timeout.Token);
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    [Fact]
    public async Task EngagementMessageAfterStartupRunsAutomationConsumerAndKeepsConsumerMatrix()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        var host = CreateHost();
        try
        {
            using var timeout = CreateTimeout();
            await host.StartAsync(timeout.Token);

            using (var scope = host.Services.CreateScope())
            {
                var registrations = scope.ServiceProvider
                    .GetServices<IIntegrationEventHandlerRegistration>()
                    .ToArray();
                Assert.Contains(registrations, registration =>
                    registration.EventType == typeof(MessageEngagementRecordedIntegrationEvent)
                    && registration.ConsumerName == MessageEngagementRecordedIntegrationEventHandler.ConsumerName);
                Assert.Contains(registrations, registration =>
                    registration.EventType == typeof(MessageEngagementRecordedIntegrationEvent)
                    && registration.ConsumerName == EngagementMessageRecordedAutomationConsumer.ConsumerName);
                Assert.Contains(registrations, registration =>
                    registration.EventType == typeof(ShopPurchaseCompletedIntegrationEvent)
                    && registration.ConsumerName == NotificationsShopPurchaseHandler.ConsumerName);
                Assert.Contains(registrations, registration =>
                    registration.EventType == typeof(ShopPurchaseCompletedIntegrationEvent)
                    && registration.ConsumerName == ShopPurchaseCompletedAutomationConsumer.ConsumerName);
            }

            var identityId = Guid.NewGuid();
            var ruleId = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 2, 21, 0, 0, TimeSpan.Zero).AddTicks(1230);
            await using (var factory = CreateFactory())
            await using (var connection = await factory.OpenConnectionAsync(timeout.Token))
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO automation_rules
                        (id, display_name, trigger_type, sort_order, is_enabled, is_archived,
                         created_at_utc, updated_at_utc)
                    VALUES
                        (@RuleId, 'Worker-Test', 'engagement.message-recorded', 0, TRUE, FALSE,
                         @Now, @Now);
                    INSERT INTO automation_rule_actions
                        (automation_rule_id, position, action_type, amount)
                    VALUES
                        (@RuleId, 0, 'economy.credit', 9);
                    """,
                    new { RuleId = ruleId, Now = now });
            }

            var messageId = await EnqueueMessageAsync(identityId, timeout.Token);
            await WaitForAutomationBalanceAsync(identityId, 9, timeout.Token);

            Assert.Equal("processed", await ReadOutboxStatusAsync(messageId, timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM automation_executions
                WHERE automation_rule_id = @RuleId AND trigger_message_id = @MessageId;
                """,
                new { RuleId = ruleId, MessageId = messageId },
                timeout.Token));
            Assert.Equal(1, await CountAsync(
                """
                SELECT COUNT(*)
                FROM flurnetz_messaging.inbox_messages
                WHERE consumer_name = @ConsumerName AND message_id = @MessageId;
                """,
                new
                {
                    ConsumerName = EngagementMessageRecordedAutomationConsumer.ConsumerName,
                    MessageId = messageId
                },
                timeout.Token));

            await StopHostAsync(host, timeout.Token);
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    [Fact]
    public async Task HostStopsWithinTheShutdownTimeout()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        var host = CreateHost();
        try
        {
            using var startTimeout = CreateTimeout();
            await host.StartAsync(startTimeout.Token);

            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var stopwatch = Stopwatch.StartNew();
            await host.StopAsync(stopTimeout.Token);
            stopwatch.Stop();

            Assert.False(stopwatch.Elapsed > TimeSpan.FromSeconds(5));
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    [Fact]
    public async Task MissingConnectionStringFailsHostStartup()
    {
        var host = Program.CreateHostBuilder([])
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FlurNetz"] = null
                }))
            .Build();
        try
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Contains("ConnectionStrings:FlurNetz", exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await DisposeHostAsync(host);
        }
    }

    private IHost CreateHost()
    {
        return Program.CreateHostBuilder([])
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FlurNetz"] = database.ConnectionString,
                    ["MessagingWorker:IdleDelay"] = "00:00:00.05",
                    ["MessagingWorker:FailureDelay"] = "00:00:00.05"
                }))
            .Build();
    }

    private async Task ResetDatabaseAsync()
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            DROP SCHEMA IF EXISTS flurnetz_messaging CASCADE;
            DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;
            DROP TABLE IF EXISTS automation_executions CASCADE;
            DROP TABLE IF EXISTS automation_rule_actions CASCADE;
            DROP TABLE IF EXISTS automation_rule_conditions CASCADE;
            DROP TABLE IF EXISTS automation_rules CASCADE;
            DROP TABLE IF EXISTS overlay_alerts CASCADE;
            DROP TABLE IF EXISTS overlay_channels CASCADE;
            DROP TABLE IF EXISTS community_notifications CASCADE;
            DROP TABLE IF EXISTS shop_purchase_requests CASCADE;
            DROP TABLE IF EXISTS shop_purchase_guards CASCADE;
            DROP TABLE IF EXISTS shop_purchases CASCADE;
            DROP TABLE IF EXISTS shop_offers CASCADE;
            DROP TABLE IF EXISTS community_progressions CASCADE;
            DROP TABLE IF EXISTS engagement_activities CASCADE;
            """);
    }

    private async Task<Guid> EnqueueMessageAsync(Guid communityIdentityId, CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        var publisher = new PostgreSqlOutboxPublisher(serializer, new SystemClock());
        var messageId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            messageId,
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion,
            DateTimeOffset.UtcNow,
            new MessageEngagementRecordedIntegrationEvent(communityIdentityId));

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(factory, cancellationToken);
        await publisher.EnqueueAsync(transaction, envelope, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messageId;
    }

    private async Task<Guid> EnqueueShopPurchaseCompletedMessageAsync(CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<ShopPurchaseCompletedIntegrationEvent>(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        var publisher = new PostgreSqlOutboxPublisher(serializer, new SystemClock());
        var messageId = Guid.NewGuid();
        var purchaseRequestId = ShopPurchaseRequestId.New();
        var occurredAtUtc = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(1230);
        var envelope = new IntegrationEventEnvelope(
            messageId,
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion,
            occurredAtUtc,
            new ShopPurchaseCompletedIntegrationEvent(
                ShopPurchaseId.New().Value,
                ShopOfferId.New().Value,
                Guid.NewGuid(),
                Guid.NewGuid(),
                25,
                occurredAtUtc),
            purchaseRequestId.Value.ToString("D"));

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(factory, cancellationToken);
        await publisher.EnqueueAsync(transaction, envelope, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messageId;
    }

    private async Task WaitForExperiencePointsAsync(
        Guid communityIdentityId,
        long expectedExperiencePoints,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var experiencePoints = await ReadExperiencePointsAsync(communityIdentityId, cancellationToken);
            if (experiencePoints == expectedExperiencePoints)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private async Task<long?> ReadExperiencePointsAsync(
        Guid communityIdentityId,
        CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                """
                SELECT experience_points
                FROM community_progressions
                WHERE community_identity_id = @CommunityIdentityId;
                """,
                new { CommunityIdentityId = communityIdentityId },
                cancellationToken: cancellationToken));
    }

    private async Task WaitForAutomationBalanceAsync(
        Guid communityIdentityId,
        long expectedBalance,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await using var factory = CreateFactory();
            await using var connection = await factory.OpenConnectionAsync(cancellationToken);
            var balance = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(
                    """
                    SELECT balance
                    FROM community_economies
                    WHERE community_identity_id = @CommunityIdentityId;
                    """,
                    new { CommunityIdentityId = communityIdentityId },
                    cancellationToken: cancellationToken));
            if (balance == expectedBalance)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private async Task<string> ReadOutboxStatusAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                "SELECT status FROM flurnetz_messaging.outbox_messages WHERE message_id = @MessageId;",
                new { MessageId = messageId },
                cancellationToken: cancellationToken));
    }

    private async Task<int> ReadOutboxAttemptCountAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT attempt_count FROM flurnetz_messaging.outbox_messages WHERE message_id = @MessageId;",
                new { MessageId = messageId },
                cancellationToken: cancellationToken));
    }

    private async Task WaitForOutboxProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var status = await ReadOutboxStatusOrNullAsync(messageId, cancellationToken);
            if (status == "processed")
            {
                return;
            }

            if (status == "failed")
            {
                throw new InvalidOperationException(
                    "The shop purchase completed message was marked as failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private async Task<string?> ReadOutboxStatusOrNullAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT status FROM flurnetz_messaging.outbox_messages WHERE message_id = @MessageId;",
                new { MessageId = messageId },
                cancellationToken: cancellationToken));
    }

    private async Task<int> CountAsync(string sql, CancellationToken cancellationToken)
    {
        return await CountAsync(sql, null, cancellationToken);
    }

    private async Task<int> CountAsync(
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
    {
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static CancellationTokenSource CreateTimeout() =>
        new(TimeSpan.FromSeconds(30));

    private static async Task StopHostAsync(IHost host, CancellationToken cancellationToken)
    {
        await host.StopAsync(cancellationToken);
    }

    private static async ValueTask DisposeHostAsync(IHost host)
    {
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        host.Dispose();
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }
}
