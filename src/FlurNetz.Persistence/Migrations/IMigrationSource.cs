namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Liefert eine zusammengehörige Quelle explizit definierter SQL-Migrationen.
/// </summary>
/// <remarks>
/// Mehrere Quellen können an einen Runner übergeben werden. Die Sammlung erfolgt
/// ausdrücklich über diesen Vertrag und benötigt weder Reflection noch Plugin-Laden.
/// </remarks>
public interface IMigrationSource
{
    /// <summary>
    /// Gibt die Migrationen dieser Quelle zur späteren globalen Sortierung zurück.
    /// </summary>
    /// <returns>Die von dieser Quelle bereitgestellten Migrationen.</returns>
    IEnumerable<Migration> GetMigrations();
}
