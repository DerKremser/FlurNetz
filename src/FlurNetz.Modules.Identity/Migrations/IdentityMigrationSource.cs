using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Identity.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des Identity-Vertical-Slices.
/// </summary>
/// <remarks>
/// Identity bleibt Eigentümer seiner einzigen fachlichen Tabelle. Die technische History
/// und die Ausführung übernimmt weiterhin der zentrale SQL-first Migration Runner.
/// </remarks>
public sealed class IdentityMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_identities
        (
            id uuid PRIMARY KEY
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige fachliche Identity-Migration zurück.
    /// </summary>
    /// <returns>Die Migration zur Anlage der internen Identitätstabelle.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Identity", 1, "CreateCommunityIdentities", MigrationSql);
    }
}
