using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Notifications.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des Notifications-Moduls.
/// </summary>
public sealed class NotificationsMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_notifications
        (
            id uuid NOT NULL,
            community_identity_id uuid NOT NULL,
            notification_type varchar(100) NOT NULL,
            title varchar(200) NOT NULL,
            message varchar(2000) NULL,
            source_type varchar(100) NULL,
            source_id varchar(200) NULL,
            created_at_utc timestamptz(6) NOT NULL,
            read_at_utc timestamptz(6) NULL,
            CONSTRAINT pk_community_notifications PRIMARY KEY (id),
            CONSTRAINT ck_community_notifications_type_length
                CHECK (char_length(notification_type) BETWEEN 1 AND 100),
            CONSTRAINT ck_community_notifications_type_trimmed
                CHECK (notification_type = btrim(notification_type)),
            CONSTRAINT ck_community_notifications_title_length
                CHECK (char_length(title) BETWEEN 1 AND 200),
            CONSTRAINT ck_community_notifications_title_trimmed
                CHECK (title = btrim(title)),
            CONSTRAINT ck_community_notifications_message_length
                CHECK (message IS NULL OR char_length(message) BETWEEN 1 AND 2000),
            CONSTRAINT ck_community_notifications_message_trimmed
                CHECK (message IS NULL OR message = btrim(message)),
            CONSTRAINT ck_community_notifications_source_pair
                CHECK ((source_type IS NULL) = (source_id IS NULL)),
            CONSTRAINT ck_community_notifications_source_type_length
                CHECK (source_type IS NULL OR char_length(source_type) BETWEEN 1 AND 100),
            CONSTRAINT ck_community_notifications_source_id_length
                CHECK (source_id IS NULL OR char_length(source_id) BETWEEN 1 AND 200)
        );

        CREATE INDEX IF NOT EXISTS ix_community_notifications_inbox
            ON community_notifications (community_identity_id, created_at_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_community_notifications_unread
            ON community_notifications (community_identity_id, created_at_utc DESC, id DESC)
            WHERE read_at_utc IS NULL;
        """;

    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration(
            "Notifications",
            1,
            "CreateCommunityNotifications",
            MigrationSql);
    }
}
