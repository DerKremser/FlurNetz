using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Overlay.Migrations;

/// <summary>Liefert die unveränderliche Overlay-V1-Migration.</summary>
public sealed class OverlayMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS overlay_channels
        (
            id uuid NOT NULL,
            display_name varchar(100) NOT NULL,
            description varchar(500) NULL,
            is_enabled boolean NOT NULL,
            is_archived boolean NOT NULL,
            created_at_utc timestamptz(6) NOT NULL,
            updated_at_utc timestamptz(6) NOT NULL,
            source_key_hash varchar(64) NULL,
            CONSTRAINT pk_overlay_channels PRIMARY KEY (id),
            CONSTRAINT ck_overlay_channels_id_not_empty CHECK (id <> '00000000-0000-0000-0000-000000000000'),
            CONSTRAINT ck_overlay_channels_not_archived_and_enabled CHECK (NOT (is_archived AND is_enabled)),
            CONSTRAINT ck_overlay_channels_display_name_length CHECK (char_length(display_name) BETWEEN 1 AND 100),
            CONSTRAINT ck_overlay_channels_display_name_not_blank CHECK (btrim(display_name, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_channels_display_name_trimmed CHECK (display_name = btrim(display_name, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_channels_description_length CHECK (description IS NULL OR char_length(description) BETWEEN 1 AND 500),
            CONSTRAINT ck_overlay_channels_description_not_blank CHECK (description IS NULL OR btrim(description, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_channels_description_trimmed CHECK (description IS NULL OR description = btrim(description, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_channels_updated_after_created CHECK (updated_at_utc >= created_at_utc),
            CONSTRAINT ck_overlay_channels_source_hash CHECK (source_key_hash IS NULL OR source_key_hash ~ '^[0-9a-f]{64}$')
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_overlay_channels_source_key_hash
            ON overlay_channels (source_key_hash) WHERE source_key_hash IS NOT NULL;

        CREATE TABLE IF NOT EXISTS overlay_alerts
        (
            id uuid NOT NULL,
            overlay_channel_id uuid NOT NULL,
            title varchar(200) NOT NULL,
            message varchar(2000) NULL,
            variant varchar(20) NOT NULL,
            duration_milliseconds integer NOT NULL,
            source_type varchar(100) NULL,
            source_id varchar(200) NULL,
            created_at_utc timestamptz(6) NOT NULL,
            expires_at_utc timestamptz(6) NOT NULL,
            CONSTRAINT pk_overlay_alerts PRIMARY KEY (id),
            CONSTRAINT fk_overlay_alerts_channel FOREIGN KEY (overlay_channel_id) REFERENCES overlay_channels (id) ON DELETE CASCADE,
            CONSTRAINT ck_overlay_alerts_ids_not_empty CHECK (id <> '00000000-0000-0000-0000-000000000000' AND overlay_channel_id <> '00000000-0000-0000-0000-000000000000'),
            CONSTRAINT ck_overlay_alerts_title_length CHECK (char_length(title) BETWEEN 1 AND 200),
            CONSTRAINT ck_overlay_alerts_title_not_blank CHECK (btrim(title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_alerts_title_trimmed CHECK (title = btrim(title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_alerts_message_length CHECK (message IS NULL OR char_length(message) BETWEEN 1 AND 2000),
            CONSTRAINT ck_overlay_alerts_message_not_blank CHECK (message IS NULL OR btrim(message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_alerts_message_trimmed CHECK (message IS NULL OR message = btrim(message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_alerts_variant CHECK (variant IN ('default', 'success', 'warning', 'celebration')),
            CONSTRAINT ck_overlay_alerts_duration CHECK (duration_milliseconds BETWEEN 1000 AND 30000),
            CONSTRAINT ck_overlay_alerts_source_pair CHECK ((source_type IS NULL) = (source_id IS NULL)),
            CONSTRAINT ck_overlay_alerts_source_type_length CHECK (source_type IS NULL OR char_length(source_type) BETWEEN 1 AND 100),
            CONSTRAINT ck_overlay_alerts_source_type_not_blank CHECK (source_type IS NULL OR btrim(source_type, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_alerts_source_type_trimmed CHECK (source_type IS NULL OR source_type = btrim(source_type, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_alerts_source_id_length CHECK (source_id IS NULL OR char_length(source_id) BETWEEN 1 AND 200),
            CONSTRAINT ck_overlay_alerts_source_id_not_blank CHECK (source_id IS NULL OR btrim(source_id, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''),
            CONSTRAINT ck_overlay_alerts_source_id_trimmed CHECK (source_id IS NULL OR source_id = btrim(source_id, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')),
            CONSTRAINT ck_overlay_alerts_expires_after_created CHECK (expires_at_utc > created_at_utc),
            CONSTRAINT ck_overlay_alerts_duration_matches_expiry CHECK (expires_at_utc = created_at_utc + make_interval(secs => duration_milliseconds / 1000.0))
        );

        CREATE INDEX IF NOT EXISTS ix_overlay_alerts_channel_order
            ON overlay_alerts (overlay_channel_id, created_at_utc ASC, id ASC);
        CREATE INDEX IF NOT EXISTS ix_overlay_alerts_channel_expiry
            ON overlay_alerts (overlay_channel_id, expires_at_utc, created_at_utc, id);
        """;

    /// <inheritdoc />
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Overlay", 1, "CreateOverlayChannelsAndAlerts", MigrationSql);
    }
}
