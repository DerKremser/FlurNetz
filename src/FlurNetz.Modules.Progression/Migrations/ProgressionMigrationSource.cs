using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Progression.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des Progression-Vertical-Slices.
/// </summary>
/// <remarks>
/// Progression bleibt Eigentümer seiner Tabelle. Die CommunityIdentityId ist ein
/// fachlicher Cross-Module-Identifier; deshalb besitzt die Tabelle bewusst keinen
/// Foreign Key auf die Identity-Tabelle.
/// </remarks>
public sealed class ProgressionMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_progressions
        (
            community_identity_id uuid PRIMARY KEY,
            experience_points bigint NOT NULL,
            CONSTRAINT ck_community_progressions_experience_points_non_negative
                CHECK (experience_points >= 0)
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige fachliche Progression-Migration zurück.
    /// </summary>
    /// <returns>Die Migration zur Anlage der Community-Progressionstabelle.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Progression", 1, "CreateCommunityProgressions", MigrationSql);
    }
}
