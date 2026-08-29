using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlurNetz.Messaging.Processing;

/// <summary>
/// Führt einen einzelnen, host-unabhängigen Outbox-Verarbeitungslauf aus.
/// </summary>
/// <remarks>
/// Der Processor startet keine Endlosschleife und implementiert keinen Worker Host. Ein API-
/// Host, Worker oder Testhost ruft <see cref="ProcessBatchAsync"/> ausdrücklich auf. Nachrichten
/// werden per PostgreSQL-Lease mit <c>FOR UPDATE SKIP LOCKED</c> geclaimt. Inbox-Eintrag und
/// Consumer-Business-Write werden in derselben Transaktion committed.
/// </remarks>
public sealed class OutboxProcessor
{
    private const string ClaimSql = """
        WITH candidates AS
        (
            SELECT message_id
            FROM flurnetz_messaging.outbox_messages
            WHERE status = 'pending'
              AND next_attempt_at_utc <= @NowUtc
              AND (locked_until_utc IS NULL OR locked_until_utc <= @NowUtc)
            ORDER BY enqueued_at_utc, message_id
            FOR UPDATE SKIP LOCKED
            LIMIT @BatchSize
        )
        UPDATE flurnetz_messaging.outbox_messages AS message
        SET attempt_count = message.attempt_count + 1,
            claimed_at_utc = @NowUtc,
            locked_until_utc = @LockedUntilUtc
        FROM candidates
        WHERE message.message_id = candidates.message_id
        RETURNING
            message.message_id AS MessageId,
            message.message_type AS MessageType,
            message.schema_version AS SchemaVersion,
            message.payload::text AS Payload,
            message.occurred_at_utc AS OccurredAtUtc,
            message.correlation_id AS CorrelationId,
            message.causation_id AS CausationId,
            message.attempt_count AS AttemptCount;
        """;

    private const string InsertInboxSql = """
        INSERT INTO flurnetz_messaging.inbox_messages
        (consumer_name, message_id, processed_at_utc)
        VALUES (@ConsumerName, @MessageId, @ProcessedAtUtc)
        ON CONFLICT (consumer_name, message_id) DO NOTHING
        RETURNING 1;
        """;

    private const string MarkProcessedSql = """
        UPDATE flurnetz_messaging.outbox_messages
        SET status = 'processed',
            processed_at_utc = @ProcessedAtUtc,
            claimed_at_utc = NULL,
            locked_until_utc = NULL,
            last_error = NULL
        WHERE message_id = @MessageId AND status = 'pending';
        """;

    private const string MarkRetrySql = """
        UPDATE flurnetz_messaging.outbox_messages
        SET status = 'pending',
            next_attempt_at_utc = @NextAttemptAtUtc,
            claimed_at_utc = NULL,
            locked_until_utc = NULL,
            last_error = @LastError
        WHERE message_id = @MessageId AND status = 'pending';
        """;

    private const string MarkFailedSql = """
        UPDATE flurnetz_messaging.outbox_messages
        SET status = 'failed',
            failed_at_utc = @FailedAtUtc,
            claimed_at_utc = NULL,
            locked_until_utc = NULL,
            last_error = @LastError
        WHERE message_id = @MessageId AND status = 'pending';
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IIntegrationEventSerializer serializer;
    private readonly IReadOnlyList<IIntegrationEventHandlerRegistration> handlers;
    private readonly OutboxProcessingOptions options;
    private readonly IClock clock;
    private readonly ILogger<OutboxProcessor> logger;

    /// <summary>
    /// Erstellt einen Outbox-Processor mit expliziten Handlern.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für PostgreSQL-Verbindungen.</param>
    /// <param name="serializer">Serializer und Registry für Outbox-Payloads.</param>
    /// <param name="registry">Registry zur Validierung der Handler-Typen.</param>
    /// <param name="handlers">Explizite Consumer-Registrierungen.</param>
    /// <param name="options">Optionale Batch-, Lease- und Retry-Grenzen.</param>
    /// <param name="clock">UTC-Zeitquelle für Claims, Retries und Statusänderungen.</param>
    /// <param name="logger">Optionaler Logger für technische Zustell- und Fehlerdiagnose.</param>
    /// <exception cref="ArgumentNullException">Wenn eine erforderliche Abhängigkeit fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn Handler doppelt oder null registriert sind.</exception>
    public OutboxProcessor(
        IPostgreSqlConnectionFactory connectionFactory,
        IIntegrationEventSerializer serializer,
        IIntegrationEventTypeRegistry registry,
        IEnumerable<IIntegrationEventHandlerRegistration> handlers,
        OutboxProcessingOptions? options = null,
        IClock? clock = null,
        ILogger<OutboxProcessor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(handlers);

        this.connectionFactory = connectionFactory;
        this.serializer = serializer;
        this.options = options ?? new OutboxProcessingOptions();
        this.options.Validate();
        this.clock = clock ?? new SystemClock();
        this.logger = logger ?? NullLogger<OutboxProcessor>.Instance;
        this.handlers = handlers.ToArray();

        if (this.handlers.Any(handler => handler is null))
        {
            throw new ArgumentException("An integration event handler registration cannot be null.", nameof(handlers));
        }

        var duplicate = this.handlers
            .GroupBy(handler => (handler.ConsumerName, handler.EventType))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"The consumer '{duplicate.Key.ConsumerName}' is registered more than once for '{duplicate.Key.EventType.FullName}'.",
                nameof(handlers));
        }

        foreach (var handler in this.handlers)
        {
            _ = registry.Resolve(handler.EventType);
        }
    }

    /// <summary>
    /// Beansprucht und verarbeitet höchstens einen Batch von Outbox-Nachrichten.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen des Laufs.</param>
    /// <returns>Das Ergebnis des einzelnen Laufs.</returns>
    public async Task<OutboxProcessingResult> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var claimedMessages = await ClaimMessagesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Claimed {MessageCount} outbox messages.", claimedMessages.Count);
        var processedCount = 0;
        var retriedCount = 0;
        var failedCount = 0;
        var duplicateDeliveryCount = 0;

        foreach (var message in claimedMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var integrationEvent = serializer.Deserialize(
                    new SerializedIntegrationEvent(
                        message.MessageType,
                        message.SchemaVersion,
                        System.Text.Encoding.UTF8.GetBytes(message.Payload)));
                var envelope = new IntegrationEventEnvelope(
                    message.MessageId,
                    message.MessageType,
                    message.SchemaVersion,
                    message.OccurredAtUtc,
                    integrationEvent,
                    message.CorrelationId,
                    message.CausationId);

                foreach (var handler in handlers.Where(handler => handler.EventType == integrationEvent.GetType()))
                {
                    if (await DeliverToHandlerAsync(message, envelope, handler, cancellationToken).ConfigureAwait(false))
                    {
                        duplicateDeliveryCount++;
                    }
                }

                await MarkProcessedAsync(message.MessageId, cancellationToken).ConfigureAwait(false);
                processedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var error = FormatError(exception);
                if (exception is UnknownIntegrationEventTypeException or UnknownIntegrationEventVersionException)
                {
                    logger.LogError(
                        "Outbox message {MessageId} has an unknown integration event type or schema version.",
                        message.MessageId);
                }

                if (message.AttemptCount >= options.MaxAttempts)
                {
                    await MarkFailedAsync(message.MessageId, error, cancellationToken).ConfigureAwait(false);
                    logger.LogError(
                        "Outbox message {MessageId} was marked as failed after {AttemptCount} attempts.",
                        message.MessageId,
                        message.AttemptCount);
                    failedCount++;
                }
                else
                {
                    await MarkRetryAsync(message.MessageId, error, cancellationToken).ConfigureAwait(false);
                    logger.LogWarning(
                        "Outbox message {MessageId} will be retried after attempt {AttemptCount}.",
                        message.MessageId,
                        message.AttemptCount);
                    retriedCount++;
                }
            }
        }

        return new OutboxProcessingResult(
            claimedMessages.Count,
            processedCount,
            retriedCount,
            failedCount,
            duplicateDeliveryCount);
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimMessagesAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        var messages = (await transaction.Connection.QueryAsync<OutboxMessage>(
                new CommandDefinition(
                    ClaimSql,
                    new
                    {
                        NowUtc = now,
                        LockedUntilUtc = now.Add(options.LeaseDuration),
                        options.BatchSize
                    },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).AsList();

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return messages;
    }

    private async Task<bool> DeliverToHandlerAsync(
        OutboxMessage message,
        IntegrationEventEnvelope envelope,
        IIntegrationEventHandlerRegistration handler,
        CancellationToken cancellationToken)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        var inserted = await transaction.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    InsertInboxSql,
                    new
                    {
                        ConsumerName = handler.ConsumerName,
                        message.MessageId,
                        ProcessedAtUtc = clock.UtcNow
                    },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (inserted is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var context = new IntegrationEventHandlerContext(envelope, transaction);
        await handler.HandleAsync(envelope.Payload, context, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await connection.ExecuteAsync(
                new CommandDefinition(
                    MarkProcessedSql,
                    new { MessageId = messageId, ProcessedAtUtc = clock.UtcNow },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task MarkRetryAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await connection.ExecuteAsync(
                new CommandDefinition(
                    MarkRetrySql,
                    new
                    {
                        MessageId = messageId,
                        NextAttemptAtUtc = clock.UtcNow.Add(options.RetryDelay),
                        LastError = error
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await connection.ExecuteAsync(
                new CommandDefinition(
                    MarkFailedSql,
                    new { MessageId = messageId, FailedAtUtc = clock.UtcNow, LastError = error },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static string FormatError(Exception exception)
    {
        return exception switch
        {
            UnknownIntegrationEventTypeException unknownType
                => $"UnknownIntegrationEventTypeException: message type '{unknownType.MessageType}' is not registered.",
            UnknownIntegrationEventVersionException unknownVersion
                => $"UnknownIntegrationEventVersionException: message type '{unknownVersion.MessageType}' has no registered schema version '{unknownVersion.SchemaVersion}'.",
            _ => $"{exception.GetType().Name}: handler processing failed."
        };
    }

    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class OutboxMessage
    {
        public Guid MessageId { get; init; }

        public string MessageType { get; init; } = string.Empty;

        public int SchemaVersion { get; init; }

        public string Payload { get; init; } = string.Empty;

        public DateTimeOffset OccurredAtUtc { get; init; }

        public string? CorrelationId { get; init; }

        public string? CausationId { get; init; }

        public int AttemptCount { get; init; }
    }
}
