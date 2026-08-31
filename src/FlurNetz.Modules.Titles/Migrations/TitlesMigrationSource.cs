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

    /// <summary>
    /// Gibt die erste Titles-Migration zurück.
    /// </summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Titles", 1, "CreateCommunityTitles", MigrationSql);
    }
}
