using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Engagement.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des Engagement-Recording-Slices.
/// </summary>
/// <remarks>
/// Engagement bleibt Eigentümer seiner Tabelle. Die CommunityIdentityId ist ein fachlicher
/// Cross-Module-Identifier; deshalb erzeugt das SQL bewusst keinen Foreign Key auf Identity.
/// </remarks>
public sealed class EngagementMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS engagement_activities
        (
            id uuid PRIMARY KEY,
            community_identity_id uuid NOT NULL,
            activity_type text NOT NULL,
            occurred_at_utc timestamptz NOT NULL
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige fachliche Engagement-Migration zurück.
    /// </summary>
    /// <returns>Die Migration zur Anlage der Engagement-Aktivitätstabelle.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Engagement", 1, "CreateEngagementActivities", MigrationSql);
    }
}
