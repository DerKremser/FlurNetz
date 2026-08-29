using System.Text;
using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Messaging.Persistence;

/// <summary>
/// Schreibt Integration Events als technische Nachrichten in die PostgreSQL-Outbox.
/// </summary>
/// <remarks>
/// Der Publisher öffnet weder eine eigene Verbindung noch eine eigene Transaktion. Diese
/// Eigenschaft ist die zentrale Atomicity-Garantie für Business Write plus Outbox Insert.
/// </remarks>
public sealed class PostgreSqlOutboxPublisher : IIntegrationEventPublisher
{
    private const string InsertSql = """
        INSERT INTO flurnetz_messaging.outbox_messages
        (
            message_id,
            message_type,
            schema_version,
            payload,
            occurred_at_utc,
            correlation_id,
            causation_id,
            enqueued_at_utc,
            status,
            attempt_count,
            next_attempt_at_utc
        )
        VALUES
        (
            @MessageId,
            @MessageType,
            @SchemaVersion,
            CAST(@Payload AS jsonb),
            @OccurredAtUtc,
            @CorrelationId,
            @CausationId,
            @EnqueuedAtUtc,
            'pending',
            0,
            @EnqueuedAtUtc
        );
        """;

    private readonly IIntegrationEventSerializer serializer;
    private readonly IClock clock;

    /// <summary>
    /// Erstellt einen Outbox-Publisher.
    /// </summary>
    /// <param name="serializer">Serializer für explizit registrierte Payload-Typen.</param>
    /// <param name="clock">UTC-Zeitquelle für den Enqueue-Zeitpunkt.</param>
    /// <exception cref="ArgumentNullException">Wenn Serializer oder Uhr fehlen.</exception>
    public PostgreSqlOutboxPublisher(
        IIntegrationEventSerializer serializer,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(clock);
        this.serializer = serializer;
        this.clock = clock;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(
        PostgreSqlTransaction transaction,
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(envelope);

        var serializedEvent = serializer.Serialize(envelope);
        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    InsertSql,
                    new
                    {
                        envelope.MessageId,
                        envelope.MessageType,
                        envelope.SchemaVersion,
                        Payload = Encoding.UTF8.GetString(serializedEvent.Payload),
                        envelope.OccurredAtUtc,
                        envelope.CorrelationId,
                        envelope.CausationId,
                        EnqueuedAtUtc = clock.UtcNow
                    },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
