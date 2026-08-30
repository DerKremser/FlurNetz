using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Economy.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des Economy-Vertical-Slices.
/// </summary>
/// <remarks>
/// Economy bleibt Eigentümer seiner Tabelle. Die CommunityIdentityId ist ein
/// fachlicher Cross-Module-Identifier und deshalb ohne Foreign Key auf Identity.
/// Sie ist zugleich der Primärschlüssel, weil aktuell genau ein Economy-Zustand
/// je Community-Identität existiert.
/// </remarks>
public sealed class EconomyMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_economies
        (
            community_identity_id uuid PRIMARY KEY,
            balance bigint NOT NULL,
            CONSTRAINT ck_community_economies_balance_non_negative
                CHECK (balance >= 0)
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige fachliche Economy-Migration zurück.
    /// </summary>
    /// <returns>Die Migration zur Anlage der Community-Economy-Tabelle.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Economy", 1, "CreateCommunityEconomies", MigrationSql);
    }
}
