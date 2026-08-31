using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Titles.Migrations;

/// <summary>
/// Liefert die erste fachliche PostgreSQL-Migration des Titles-Vertical-Slices.
/// </summary>
/// <remarks>
/// Titles besitzt und verknüpft ausschließlich seine drei eigenen Tabellen. Die
/// CommunityIdentityId bleibt ein fachlicher Cross-Module-Identifier ohne Foreign Key
/// auf die Identity-Persistenz.
/// </remarks>
public sealed class TitlesMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_titles
        (
            community_identity_id uuid NOT NULL,
            CONSTRAINT pk_community_titles
                PRIMARY KEY (community_identity_id)
        );

        CREATE TABLE IF NOT EXISTS community_title_unlocks
        (
            community_identity_id uuid NOT NULL,
            title_definition_id uuid NOT NULL,
            CONSTRAINT pk_community_title_unlocks
                PRIMARY KEY (community_identity_id, title_definition_id),
            CONSTRAINT fk_community_title_unlocks_community_titles
                FOREIGN KEY (community_identity_id)
                REFERENCES community_titles (community_identity_id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS community_title_selections
        (
            community_identity_id uuid NOT NULL,
            title_definition_id uuid NOT NULL,
            CONSTRAINT pk_community_title_selections
                PRIMARY KEY (community_identity_id),
            CONSTRAINT fk_community_title_selections_community_titles
                FOREIGN KEY (community_identity_id)
                REFERENCES community_titles (community_identity_id)
                ON DELETE CASCADE,
            CONSTRAINT fk_community_title_selections_unlock
                FOREIGN KEY (community_identity_id, title_definition_id)
                REFERENCES community_title_unlocks
                    (community_identity_id, title_definition_id)
        );
        """;

    private const string TitleDefinitionsMigrationSql = """
        CREATE TABLE IF NOT EXISTS title_definitions
        (
            id uuid NOT NULL,
            display_name varchar(100) NOT NULL,
            description varchar(500) NULL,

            CONSTRAINT pk_title_definitions
                PRIMARY KEY (id),

            CONSTRAINT ck_title_definitions_display_name_not_blank
                CHECK (btrim(display_name) <> ''),

            CONSTRAINT ck_title_definitions_display_name_trimmed
                CHECK (display_name = btrim(display_name)),

            CONSTRAINT ck_title_definitions_description_not_blank
                CHECK (
                    description IS NULL
                    OR btrim(description) <> ''
                ),

            CONSTRAINT ck_title_definitions_description_trimmed
                CHECK (
                    description IS NULL
                    OR description = btrim(description)
                )
        );
        """;

    /// <summary>
    /// Gibt die unveränderte Community-State-Migration und den Katalog an.
    /// </summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Titles", 1, "CreateCommunityTitles", MigrationSql);
        yield return new Migration(
            "Titles",
            2,
            "CreateTitleDefinitions",
            TitleDefinitionsMigrationSql);
    }
}
