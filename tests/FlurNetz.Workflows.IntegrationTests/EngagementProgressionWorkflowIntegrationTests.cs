using System.Text.Json;
using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Processing;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Engagement.Migrations;
using FlurNetz.Modules.Engagement.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Progression.Migrations;
using FlurNetz.Modules.Progression.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Workflows.IntegrationTests;

/// <summary>
/// Prüft den ersten echten Engagement-zu-Progression-Workflow Ende zu Ende.
/// </summary>
public sealed class EngagementProgressionWorkflowIntegrationTests(WorkflowPostgreSqlFixture database)
    : IClassFixture<WorkflowPostgreSqlFixture>
{
    private static readonly DateTimeOffset TestNow =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MessageActivityAndOutboxAreProcessedToExactlyOneExperiencePoint()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareWorkflowAsync(factory);
        var workflow = CreateWorkflow(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var activityId = await workflow.Engagement.ExecuteAsync(communityIdentityId, TestToken);
        var activity = await workflow.Repository.GetByIdAsync(activityId, TestToken);
        var outbox = await ReadSingleOutboxMessageAsync(factory);
        var identityTableExists = await CountAsync(
            factory,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'community_identities';");

        Assert.Equal(0, identityTableExists);
        Assert.NotNull(activity);
        Assert.Equal(communityIdentityId, activity!.CommunityIdentityId);
        Assert.Equal(EngagementActivityType.Message, activity.Type);
        Assert.Equal(TestNow, activity.OccurredAtUtc);
        Assert.Equal("pending", outbox.Status);
        Assert.Equal(MessageEngagementRecordedIntegrationEvent.MessageType, outbox.MessageType);
        Assert.Equal(MessageEngagementRecordedIntegrationEvent.SchemaVersion, outbox.SchemaVersion);
        Assert.Equal(activity.OccurredAtUtc, outbox.OccurredAtUtc);
        Assert.Null(outbox.CorrelationId);
        Assert.Null(outbox.CausationId);
        Assert.NotEqual(Guid.Empty, outbox.MessageId);
        AssertPayloadContainsOnlyCommunityIdentity(outbox.Payload, communityIdentityId.Value);

        var result = await workflow.Processor.ProcessBatchAsync(TestToken);

        var loadedProgression = await workflow.ProgressionStore
            .GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);
        var processedOutbox = await ReadSingleOutboxMessageAsync(factory);
        var inbox = await ReadSingleInboxMessageAsync(factory);
        var persistedActivity = await workflow.Repository.GetByIdAsync(activityId, TestToken);

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(0, result.DuplicateDeliveryCount);
        Assert.Equal("processed", processedOutbox.Status);
        Assert.Equal(MessageEngagementRecordedIntegrationEventHandler.ConsumerName, inbox.ConsumerName);
        Assert.Equal(outbox.MessageId, inbox.MessageId);
        Assert.Equal(1, loadedProgression?.ExperiencePoints.Value);
        Assert.NotNull(persistedActivity);
        Assert.Equal(activityId, persistedActivity!.Id);
        Assert.Equal(activity.CommunityIdentityId, persistedActivity.CommunityIdentityId);
        Assert.Equal(activity.Type, persistedActivity.Type);
        Assert.Equal(activity.OccurredAtUtc, persistedActivity.OccurredAtUtc);
    }

    [Fact]
    public async Task ProducerRollsBackActivityAndOutboxWhenEnqueueFails()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareWorkflowAsync(factory);
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var realPublisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var publisher = new FailingAfterEnqueuePublisher(realPublisher);
        var repository = new EngagementActivityRepository(factory);
        var recorder = new PostgreSqlMessageEngagementRecorder(factory, repository, publisher);
        var useCase = new RecordMessageEngagement(recorder, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(CommunityIdentityId.New(), TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM engagement_activities;", cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM flurnetz_messaging.outbox_messages;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DuplicateDeliveryUsesInboxIdempotencyAndDoesNotGrantExperienceTwice()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareWorkflowAsync(factory);
        var workflow = CreateWorkflow(factory);
        var communityIdentityId = CommunityIdentityId.New();
        await workflow.Engagement.ExecuteAsync(communityIdentityId, TestToken);
        var firstRun = await workflow.Processor.ProcessBatchAsync(TestToken);

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE flurnetz_messaging.outbox_messages SET status = 'pending', processed_at_utc = NULL, next_attempt_at_utc = @NowUtc, last_error = NULL;",
                    new { NowUtc = TestNow },
                    cancellationToken: TestToken));
        }

        var duplicateRun = await workflow.Processor.ProcessBatchAsync(TestToken);
        var progression = await workflow.ProgressionStore
            .GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);
        var inboxCount = await CountAsync(factory, "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages;");
        var activityCount = await CountAsync(factory, "SELECT COUNT(*) FROM engagement_activities;");

        Assert.Equal(1, firstRun.ProcessedCount);
        Assert.Equal(1, duplicateRun.ProcessedCount);
        Assert.Equal(1, duplicateRun.DuplicateDeliveryCount);
        Assert.Equal(1, progression?.ExperiencePoints.Value);
        Assert.Equal(1, inboxCount);
        Assert.Equal(1, activityCount);
    }

    [Fact]
    public async Task ConsumerFailureRollsBackInboxAndProgressionAndRetrySucceeds()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareWorkflowAsync(factory);
        var workflow = CreateWorkflow(factory, maxAttempts: 3);
        var communityIdentityId = CommunityIdentityId.New();
        await workflow.Engagement.ExecuteAsync(communityIdentityId, TestToken);

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO community_progressions (community_identity_id, experience_points) VALUES (@CommunityIdentityId, @ExperiencePoints);",
                    new
                    {
                        CommunityIdentityId = communityIdentityId.Value,
                        ExperiencePoints = long.MaxValue
                    },
                    cancellationToken: TestToken));
        }

        var failedRun = await workflow.Processor.ProcessBatchAsync(TestToken);
        var failedOutbox = await ReadSingleOutboxMessageAsync(factory);
        var failedProgression = await workflow.ProgressionStore
            .GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);
        var failedInboxCount = await CountAsync(factory, "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages;");

        Assert.Equal(1, failedRun.RetriedCount);
        Assert.Equal(0, failedRun.ProcessedCount);
        Assert.Equal("pending", failedOutbox.Status);
        Assert.Equal(1, failedOutbox.AttemptCount);
        Assert.Contains("OverflowException", failedOutbox.LastError, StringComparison.Ordinal);
        Assert.Equal(long.MaxValue, failedProgression?.ExperiencePoints.Value);
        Assert.Equal(0, failedInboxCount);

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE community_progressions SET experience_points = 0 WHERE community_identity_id = @CommunityIdentityId;",
                    new { CommunityIdentityId = communityIdentityId.Value },
                    cancellationToken: TestToken));
        }

        var successfulRetry = await workflow.Processor.ProcessBatchAsync(TestToken);
        var retriedProgression = await workflow.ProgressionStore
            .GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);
        var retriedOutbox = await ReadSingleOutboxMessageAsync(factory);

        Assert.Equal(1, successfulRetry.ProcessedCount);
        Assert.Equal(1, retriedProgression?.ExperiencePoints.Value);
        Assert.Equal("processed", retriedOutbox.Status);
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM flurnetz_messaging.inbox_messages;"));
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareWorkflowAsync(PostgreSqlConnectionFactory factory)
    {
        var migrationSources = CreateMigrationSources();
        await new MigrationRunner(factory, migrationSources).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM flurnetz_messaging.inbox_messages; DELETE FROM flurnetz_messaging.outbox_messages; DELETE FROM engagement_activities; DELETE FROM community_progressions;",
                cancellationToken: TestToken));
    }

    private static IEnumerable<IMigrationSource> CreateMigrationSources() =>
    [
        new MessagingMigrationSource(),
        new EngagementMigrationSource(),
        new ProgressionMigrationSource()
    ];

    private static WorkflowComponents CreateWorkflow(
        PostgreSqlConnectionFactory factory,
        int maxAttempts = 3)
    {
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var repository = new EngagementActivityRepository(factory);
        var recorder = new PostgreSqlMessageEngagementRecorder(factory, repository, publisher);
        var engagement = new RecordMessageEngagement(recorder, clock);
        var progressionStore = new CommunityProgressionStore(factory);
        var handler = new MessageEngagementRecordedIntegrationEventHandler(progressionStore);
        var registration = new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEventHandler.ConsumerName,
            handler);
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [registration],
            new OutboxProcessingOptions
            {
                BatchSize = 100,
                MaxAttempts = maxAttempts,
                RetryDelay = TimeSpan.Zero,
                LeaseDuration = TimeSpan.FromMinutes(5)
            },
            clock);

        return new WorkflowComponents(engagement, repository, progressionStore, processor);
    }

    private static (IntegrationEventTypeRegistry Registry, IntegrationEventJsonSerializer Serializer) CreateSerializer()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        return (registry, new IntegrationEventJsonSerializer(registry));
    }

    private static async Task<OutboxMessage> ReadSingleOutboxMessageAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<OutboxMessage>(
            new CommandDefinition(
                """
                SELECT message_id AS MessageId,
                       message_type AS MessageType,
                       schema_version AS SchemaVersion,
                       payload::text AS Payload,
                       occurred_at_utc AS OccurredAtUtc,
                       correlation_id AS CorrelationId,
                       causation_id AS CausationId,
                       status AS Status,
                       attempt_count AS AttemptCount,
                       last_error AS LastError
                FROM flurnetz_messaging.outbox_messages;
                """,
                cancellationToken: TestToken));
    }

    private static async Task<InboxMessage> ReadSingleInboxMessageAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<InboxMessage>(
            new CommandDefinition(
                "SELECT consumer_name AS ConsumerName, message_id AS MessageId FROM flurnetz_messaging.inbox_messages;",
                cancellationToken: TestToken));
    }

    private static async Task<int> CountAsync(PostgreSqlConnectionFactory factory, string sql)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, cancellationToken: TestToken));
    }

    private static void AssertPayloadContainsOnlyCommunityIdentity(string payload, Guid expectedIdentity)
    {
        using var document = JsonDocument.Parse(payload);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.Single(properties);
        Assert.Equal("communityIdentityId", properties[0].Name);
        Assert.Equal(expectedIdentity, properties[0].Value.GetGuid());
    }

    private sealed record WorkflowComponents(
        RecordMessageEngagement Engagement,
        EngagementActivityRepository Repository,
        CommunityProgressionStore ProgressionStore,
        OutboxProcessor Processor);

    private sealed class OutboxMessage
    {
        public Guid MessageId { get; set; }

        public string MessageType { get; set; } = string.Empty;

        public int SchemaVersion { get; set; }

        public string Payload { get; set; } = string.Empty;

        public DateTimeOffset OccurredAtUtc { get; set; }

        public string? CorrelationId { get; set; }

        public string? CausationId { get; set; }

        public string Status { get; set; } = string.Empty;

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }
    }

    private sealed class InboxMessage
    {
        public string ConsumerName { get; set; } = string.Empty;

        public Guid MessageId { get; set; }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FailingAfterEnqueuePublisher(IIntegrationEventPublisher inner)
        : IIntegrationEventPublisher
    {
        public async Task EnqueueAsync(
            PostgreSqlTransaction transaction,
            IntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            await inner.EnqueueAsync(transaction, envelope, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Gezielt ausgelöster Outbox-Fehler nach dem INSERT.");
        }
    }
}
