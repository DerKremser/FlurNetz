using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Messaging.Migrations;

/// <summary>
/// Liefert die technischen PostgreSQL-Migrationen der Messaging Foundation.
/// </summary>
/// <remarks>
/// Messaging registriert seine Tabellen ausdrücklich beim bestehenden SQL-first Migration
/// Runner. Der eindeutige Owner ist <c>Messaging</c>; Fachmodule besitzen keine dieser Tabellen.
/// </remarks>
public sealed class MessagingMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE SCHEMA IF NOT EXISTS flurnetz_messaging;

        CREATE TABLE IF NOT EXISTS flurnetz_messaging.outbox_messages
        (
            message_id uuid NOT NULL,
            message_type text NOT NULL,
            schema_version integer NOT NULL,
            payload jsonb NOT NULL,
            occurred_at_utc timestamp with time zone NOT NULL,
            correlation_id text NULL,
            causation_id text NULL,
            enqueued_at_utc timestamp with time zone NOT NULL,
            status text NOT NULL DEFAULT 'pending',
            attempt_count integer NOT NULL DEFAULT 0,
            next_attempt_at_utc timestamp with time zone NOT NULL,
            claimed_at_utc timestamp with time zone NULL,
            locked_until_utc timestamp with time zone NULL,
            processed_at_utc timestamp with time zone NULL,
            failed_at_utc timestamp with time zone NULL,
            last_error text NULL,
            CONSTRAINT pk_outbox_messages PRIMARY KEY (message_id),
            CONSTRAINT ck_outbox_schema_version_positive CHECK (schema_version > 0),
            CONSTRAINT ck_outbox_attempt_count_nonnegative CHECK (attempt_count >= 0),
            CONSTRAINT ck_outbox_status CHECK (status IN ('pending', 'processed', 'failed'))
        );

        CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending
            ON flurnetz_messaging.outbox_messages (next_attempt_at_utc, enqueued_at_utc, message_id)
            WHERE status = 'pending';

        CREATE TABLE IF NOT EXISTS flurnetz_messaging.inbox_messages
        (
            consumer_name text NOT NULL,
            message_id uuid NOT NULL,
            processed_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT pk_inbox_messages PRIMARY KEY (consumer_name, message_id)
        );
        """;

    /// <inheritdoc />
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Messaging", 1, "CreateOutboxAndInbox", MigrationSql);
    }
}
