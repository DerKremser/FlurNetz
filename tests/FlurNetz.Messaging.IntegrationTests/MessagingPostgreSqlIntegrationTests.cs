using System.Data.Common;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Processing;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Messaging.IntegrationTests;

/// <summary>
/// Prüft Outbox, Inbox, Retry und Transaktionsgrenzen mit echtem PostgreSQL.
/// </summary>
public sealed class MessagingPostgreSqlIntegrationTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task MessagingMigrationCreatesTablesAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetMessagingSchemaAsync(factory, TestContext.Current.CancellationToken);

        var runner = new MigrationRunner(factory, new MessagingMigrationSource());
        var firstRun = await runner.RunAsync(TestContext.Current.CancellationToken);
        var secondRun = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);
        Assert.True(await TableExistsAsync(factory, "flurnetz_messaging.outbox_messages", TestContext.Current.CancellationToken));
        Assert.True(await TableExistsAsync(factory, "flurnetz_messaging.inbox_messages", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BusinessWriteAndOutboxEnqueueCommitAtomically()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("atomic_commit");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
            {
                await ExecuteAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    $"INSERT INTO {table} (value) VALUES (@value);",
                    TestToken,
                    ("value", "business"));
                await publisher.EnqueueAsync(
                    transaction,
                    CreateEnvelope(messageId, "business"),
                    TestToken);
                await transaction.CommitAsync(TestToken);
            }

            Assert.Equal(1, await CountAsync(factory, table, TestToken));
            Assert.Equal(1, await CountAsync(factory, "flurnetz_messaging.outbox_messages", TestToken, "message_id = @message_id", ("message_id", messageId)));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task BusinessWriteAndOutboxEnqueueRollbackTogether()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("atomic_rollback");
        var clock = new FixedClock(TestNow);
        var (_, serializer) = CreateSerializer();
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
            {
                await ExecuteAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    $"INSERT INTO {table} (value) VALUES (@value);",
                    TestToken,
                    ("value", "rolled-back"));
                await publisher.EnqueueAsync(
                    transaction,
                    CreateEnvelope(messageId, "rolled-back"),
                    TestToken);
                await transaction.RollbackAsync(TestToken);
            }

            Assert.Equal(0, await CountAsync(factory, table, TestToken));
            Assert.Equal(0, await CountAsync(factory, "flurnetz_messaging.outbox_messages", TestToken, "message_id = @message_id", ("message_id", messageId)));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task OutboxProcessorDeliversOnceAndMarksMessageProcessed()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("processed");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var handler = new SyntheticHandler(table);
        var processor = CreateProcessor(factory, registry, serializer, handler, clock);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await EnqueueAsync(factory, publisher, CreateEnvelope(messageId, "once"));

            var result = await processor.ProcessBatchAsync(TestToken);

            Assert.Equal(1, result.ClaimedCount);
            Assert.Equal(1, result.ProcessedCount);
            Assert.Equal(1, await CountAsync(factory, table, TestToken));
            Assert.Equal("processed", await ReadStringAsync(factory, "SELECT status FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;", TestToken, ("message_id", messageId)));
            Assert.Equal(1, await CountAsync(factory, "flurnetz_messaging.inbox_messages", TestToken, "message_id = @message_id", ("message_id", messageId)));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task KnownMessageWithoutConsumerIsProcessedWithoutRetryOrInboxEntry()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [],
            new OutboxProcessingOptions
            {
                BatchSize = 100,
                MaxAttempts = 2,
                RetryDelay = TimeSpan.Zero,
                LeaseDuration = TimeSpan.FromMinutes(5)
            },
            clock);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        await EnqueueAsync(factory, publisher, CreateEnvelope(messageId, "no-consumer"));

        var result = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(
            "processed",
            await ReadStringAsync(
                factory,
                "SELECT status FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;",
                TestToken,
                ("message_id", messageId)));
        Assert.Equal(
            0,
            await CountAsync(
                factory,
                "flurnetz_messaging.inbox_messages",
                TestToken,
                "message_id = @message_id",
                ("message_id", messageId)));
    }

    [Fact]
    public async Task DuplicateRedeliveryIsDeduplicatedByConsumerIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("duplicate");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var handler = new SyntheticHandler(table);
        var processor = CreateProcessor(factory, registry, serializer, handler, clock);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await EnqueueAsync(factory, publisher, CreateEnvelope(messageId, "duplicate"));
            await processor.ProcessBatchAsync(TestToken);
            await ExecuteOnConnectionAsync(
                factory,
                "UPDATE flurnetz_messaging.outbox_messages SET status = 'pending', processed_at_utc = NULL, next_attempt_at_utc = @now, last_error = NULL WHERE message_id = @message_id;",
                TestToken,
                ("now", TestNow),
                ("message_id", messageId));

            var redelivery = await processor.ProcessBatchAsync(TestToken);

            Assert.Equal(1, redelivery.DuplicateDeliveryCount);
            Assert.Equal(1, await CountAsync(factory, table, TestToken));
            Assert.Equal(1, await CountAsync(factory, "flurnetz_messaging.inbox_messages", TestToken, "consumer_name = @consumer_name AND message_id = @message_id", ("consumer_name", "synthetic-consumer"), ("message_id", messageId)));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task TransactionalInboxRollsBackBusinessWriteAndRetriesSuccessfully()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("retry");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var handler = new SyntheticHandler(table) { FailNextAfterWrite = true };
        var processor = CreateProcessor(factory, registry, serializer, handler, clock);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await EnqueueAsync(factory, publisher, CreateEnvelope(messageId, "retry"));

            var failedAttempt = await processor.ProcessBatchAsync(TestToken);
            Assert.Equal(1, failedAttempt.RetriedCount);
            Assert.Equal(1, await ReadIntAsync(factory, "SELECT attempt_count FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;", TestToken, ("message_id", messageId)));
            Assert.Equal(0, await CountAsync(factory, table, TestToken));
            Assert.Equal(0, await CountAsync(factory, "flurnetz_messaging.inbox_messages", TestToken, "message_id = @message_id", ("message_id", messageId)));

            var successfulAttempt = await processor.ProcessBatchAsync(TestToken);
            Assert.Equal(1, successfulAttempt.ProcessedCount);
            Assert.Equal(1, await CountAsync(factory, table, TestToken));
            Assert.Equal(1, await CountAsync(factory, "flurnetz_messaging.inbox_messages", TestToken, "message_id = @message_id", ("message_id", messageId)));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task PoisonMessageBecomesFailedAndDoesNotBlockAnotherMessage()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var table = NewTableName("poison");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var handler = new SyntheticHandler(table);
        var processor = CreateProcessor(factory, registry, serializer, handler, clock, maxAttempts: 2);
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var poisonId = Guid.NewGuid();
        var goodId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(factory, table);
            await EnqueueAsync(factory, publisher, CreateEnvelope(poisonId, "poison", shouldFail: true));
            await EnqueueAsync(factory, publisher, CreateEnvelope(goodId, "good"));

            var firstRun = await processor.ProcessBatchAsync(TestToken);
            var secondRun = await processor.ProcessBatchAsync(TestToken);
            var thirdRun = await processor.ProcessBatchAsync(TestToken);

            Assert.Equal(1, firstRun.ProcessedCount);
            Assert.Equal(1, firstRun.RetriedCount);
            Assert.Equal(1, secondRun.FailedCount);
            Assert.Equal(0, thirdRun.ClaimedCount);
            Assert.Equal("failed", await ReadStringAsync(factory, "SELECT status FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;", TestToken, ("message_id", poisonId)));
            Assert.Contains("InvalidOperationException", await ReadStringAsync(factory, "SELECT last_error FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;", TestToken, ("message_id", poisonId)));
            Assert.Equal(1, await CountAsync(factory, table, TestToken));
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task UnknownMessageTypeUsesRetryAndPoisonHandling()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareMessagingAsync(factory);
        var clock = new FixedClock(TestNow);
        var registry = new IntegrationEventTypeRegistry();
        var serializer = new IntegrationEventJsonSerializer(registry);
        var processor = new OutboxProcessor(
            factory,
            serializer,
            registry,
            [],
            new OutboxProcessingOptions { MaxAttempts = 2, RetryDelay = TimeSpan.Zero },
            clock);
        var messageId = Guid.NewGuid();

        await ExecuteOnConnectionAsync(
            factory,
            """
            INSERT INTO flurnetz_messaging.outbox_messages
                (message_id, message_type, schema_version, payload, occurred_at_utc, enqueued_at_utc, next_attempt_at_utc)
            VALUES (@message_id, 'unknown.synthetic', 1, '{}'::jsonb, @now, @now, @now);
            """,
            TestToken,
            ("message_id", messageId),
            ("now", TestNow));

        var firstRun = await processor.ProcessBatchAsync(TestToken);
        var secondRun = await processor.ProcessBatchAsync(TestToken);

        Assert.Equal(1, firstRun.RetriedCount);
        Assert.Equal(1, secondRun.FailedCount);
        Assert.Contains("not registered", await ReadStringAsync(factory, "SELECT last_error FROM flurnetz_messaging.outbox_messages WHERE message_id = @message_id;", TestToken, ("message_id", messageId)));
    }

    [Fact]
    public async Task ParallelProcessorsClaimOneMessageAndCreateOneBusinessEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var firstFactory = CreateFactory();
        await using var secondFactory = CreateFactory();
        await PrepareMessagingAsync(firstFactory);
        var table = NewTableName("parallel");
        var clock = new FixedClock(TestNow);
        var (registry, serializer) = CreateSerializer();
        var publisher = new PostgreSqlOutboxPublisher(serializer, clock);
        var messageId = Guid.NewGuid();

        try
        {
            await CreateBusinessTableAsync(firstFactory, table);
            await EnqueueAsync(firstFactory, publisher, CreateEnvelope(messageId, "parallel"));
            var handler = new SyntheticHandler(table) { Delay = TimeSpan.FromMilliseconds(100) };
            var firstProcessor = CreateProcessor(firstFactory, registry, serializer, handler, clock);
            var secondProcessor = CreateProcessor(secondFactory, registry, serializer, handler, clock);

            var results = await Task.WhenAll(
                firstProcessor.ProcessBatchAsync(TestToken),
                secondProcessor.ProcessBatchAsync(TestToken));

            Assert.Equal(1, results.Sum(result => result.ClaimedCount));
            Assert.Equal(1, results.Sum(result => result.ProcessedCount));
            Assert.Equal(1, await CountAsync(firstFactory, table, TestToken));
        }
        finally
        {
            await DropTableAsync(firstFactory, table);
        }
    }

    private static readonly DateTimeOffset TestNow = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken TestToken => TestContext.Current.CancellationToken;

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private async Task PrepareMessagingAsync(PostgreSqlConnectionFactory factory)
    {
        var runner = new MigrationRunner(factory, new MessagingMigrationSource());
        await runner.RunAsync(TestToken);
        await ExecuteOnConnectionAsync(
            factory,
            "DELETE FROM flurnetz_messaging.inbox_messages; DELETE FROM flurnetz_messaging.outbox_messages;",
            TestToken);
    }

    private static async Task ResetMessagingSchemaAsync(PostgreSqlConnectionFactory factory, CancellationToken cancellationToken)
    {
        await ExecuteOnConnectionAsync(
            factory,
            "DROP SCHEMA IF EXISTS flurnetz_messaging CASCADE; DELETE FROM flurnetz_persistence.migration_history WHERE owner = 'Messaging' AND version = 1;",
            cancellationToken);
    }

    private static (IntegrationEventTypeRegistry Registry, IntegrationEventJsonSerializer Serializer) CreateSerializer()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<SyntheticIntegrationEvent>("test.synthetic", 1);
        return (registry, new IntegrationEventJsonSerializer(registry));
    }

    private static OutboxProcessor CreateProcessor(
        PostgreSqlConnectionFactory factory,
        IntegrationEventTypeRegistry registry,
        IntegrationEventJsonSerializer serializer,
        SyntheticHandler handler,
        IClock clock,
        int maxAttempts = 3)
    {
        return new OutboxProcessor(
            factory,
            serializer,
            registry,
            [new IntegrationEventHandlerRegistration<SyntheticIntegrationEvent>("synthetic-consumer", handler)],
            new OutboxProcessingOptions
            {
                BatchSize = 100,
                MaxAttempts = maxAttempts,
                RetryDelay = TimeSpan.Zero,
                LeaseDuration = TimeSpan.FromMinutes(5)
            },
            clock);
    }

    private static IntegrationEventEnvelope CreateEnvelope(Guid messageId, string value, bool shouldFail = false)
    {
        return new IntegrationEventEnvelope(
            messageId,
            "test.synthetic",
            1,
            TestNow,
            new SyntheticIntegrationEvent(value, shouldFail));
    }

    private static async Task EnqueueAsync(
        PostgreSqlConnectionFactory factory,
        PostgreSqlOutboxPublisher publisher,
        IntegrationEventEnvelope envelope)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(factory, CancellationToken.None);
        await publisher.EnqueueAsync(transaction, envelope, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    private static async Task CreateBusinessTableAsync(PostgreSqlConnectionFactory factory, string table)
    {
        await ExecuteOnConnectionAsync(
            factory,
            $"CREATE TABLE {table} (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, value text NOT NULL);",
            CancellationToken.None);
    }

    private static async Task DropTableAsync(PostgreSqlConnectionFactory factory, string table)
    {
        await ExecuteOnConnectionAsync(factory, $"DROP TABLE IF EXISTS {table};", CancellationToken.None);
    }

    private static async Task<bool> TableExistsAsync(PostgreSqlConnectionFactory factory, string table, CancellationToken cancellationToken)
    {
        return await ReadBoolAsync(
            factory,
            "SELECT to_regclass(@table_name) IS NOT NULL;",
            cancellationToken,
            ("table_name", table));
    }

    private static async Task<int> CountAsync(
        PostgreSqlConnectionFactory factory,
        string table,
        CancellationToken cancellationToken,
        string? predicate = null,
        params (string Name, object? Value)[] parameters)
    {
        var sql = $"SELECT COUNT(*) FROM {table}" + (predicate is null ? ";" : $" WHERE {predicate};");
        return await ReadIntAsync(factory, sql, cancellationToken, parameters);
    }

    private static async Task<int> ReadIntAsync(
        PostgreSqlConnectionFactory factory,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var value = await ReadScalarAsync(factory, sql, cancellationToken, parameters);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> ReadStringAsync(
        PostgreSqlConnectionFactory factory,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var value = await ReadScalarAsync(factory, sql, cancellationToken, parameters);
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("The database returned no text value.");
    }

    private static async Task<bool> ReadBoolAsync(
        PostgreSqlConnectionFactory factory,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        return Convert.ToBoolean(await ReadScalarAsync(factory, sql, cancellationToken, parameters), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<object?> ReadScalarAsync(
        PostgreSqlConnectionFactory factory,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, null, sql, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteOnConnectionAsync(
        PostgreSqlConnectionFactory factory,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, null, sql, cancellationToken, parameters);
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        IReadOnlyCollection<(string Name, object? Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string NewTableName(string purpose) => $"flurnetz_test_{purpose}_{Guid.NewGuid():N}";

    private sealed record SyntheticIntegrationEvent(string Value, bool ShouldFail) : IIntegrationEvent;

    private sealed class SyntheticHandler(string table) : IIntegrationEventHandler<SyntheticIntegrationEvent>
    {
        public bool FailNextAfterWrite { get; set; }

        public TimeSpan Delay { get; set; }

        public async Task HandleAsync(
            SyntheticIntegrationEvent @event,
            IntegrationEventHandlerContext context,
            CancellationToken cancellationToken = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (@event.ShouldFail)
            {
                throw new InvalidOperationException("synthetic poison failure");
            }

            await ExecuteAsync(
                context.Connection,
                context.Transaction,
                $"INSERT INTO {table} (value) VALUES (@value);",
                cancellationToken,
                ("value", @event.Value));

            if (FailNextAfterWrite)
            {
                FailNextAfterWrite = false;
                throw new InvalidOperationException("synthetic transactional failure");
            }
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
