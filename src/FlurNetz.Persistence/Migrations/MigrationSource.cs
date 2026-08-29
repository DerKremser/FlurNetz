namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Stellt eine konkrete Sammlung von Migrationen als Migrationsquelle bereit.
/// </summary>
public sealed class MigrationSource : IMigrationSource
{
    private readonly IReadOnlyList<Migration> migrations;

    /// <summary>
    /// Erstellt eine Quelle und materialisiert ihre Migrationen zur stabilen Verwendung.
    /// </summary>
    /// <param name="migrations">Die Migrationen dieser Quelle.</param>
    /// <exception cref="ArgumentNullException">Wenn die Sammlung fehlt.</exception>
    public MigrationSource(IEnumerable<Migration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        this.migrations = migrations.ToArray();
    }

    /// <summary>
    /// Gibt die materialisierten Migrationen dieser Quelle zurück.
    /// </summary>
    /// <returns>Die unveränderte Reihenfolge der bereitgestellten Migrationen.</returns>
    public IEnumerable<Migration> GetMigrations() => migrations;
}
