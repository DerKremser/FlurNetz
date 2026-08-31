using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Achievements.Migrations;

/// <summary>
/// Liefert die Achievements-eigene PostgreSQL-Migration für Katalog und Community-Unlocks.
/// </summary>
public sealed class AchievementsMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS achievement_definitions
        (
            id uuid NOT NULL,
            display_name varchar(100) NOT NULL,
            description varchar(500) NULL,

            CONSTRAINT pk_achievement_definitions
                PRIMARY KEY (id),

            CONSTRAINT ck_achievement_definitions_display_name_not_blank
                CHECK (
                    btrim(
                        display_name,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    ) <> ''
                ),

            CONSTRAINT ck_achievement_definitions_display_name_trimmed
                CHECK (
                    display_name = btrim(
                        display_name,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    )
                ),

            CONSTRAINT ck_achievement_definitions_description_not_blank
                CHECK (
                    description IS NULL
                    OR btrim(
                        description,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    ) <> ''
                ),

            CONSTRAINT ck_achievement_definitions_description_trimmed
                CHECK (
                    description IS NULL
                    OR description = btrim(
                        description,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    )
                )
        );

        CREATE TABLE IF NOT EXISTS community_achievements
        (
            community_identity_id uuid NOT NULL,
            achievement_definition_id uuid NOT NULL,
            unlocked_at_utc timestamptz NOT NULL,

            CONSTRAINT pk_community_achievements
                PRIMARY KEY (community_identity_id, achievement_definition_id),

            CONSTRAINT fk_community_achievements_definition
                FOREIGN KEY (achievement_definition_id)
                REFERENCES achievement_definitions (id)
        );
        """;

    /// <summary>
    /// Gibt die erste Achievements-Migration zurück.
    /// </summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration(
            "Achievements",
            1,
            "CreateAchievementDefinitionsAndCommunityAchievements",
            MigrationSql);
    }
}
