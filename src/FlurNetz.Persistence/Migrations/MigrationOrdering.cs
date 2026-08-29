namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Validiert und sortiert Migrationen vor jedem Datenbankzugriff deterministisch.
/// </summary>
public static class MigrationOrdering
{
    /// <summary>
    /// Materialisiert Migrationen, weist doppelte Besitzer-Version-Schlüssel zurück und sortiert sie.
    /// </summary>
    /// <param name="migrations">Die aus einer oder mehreren Quellen gesammelten Migrationen.</param>
    /// <returns>Migrationen nach Owner, Version und Name mit ordinalem String-Vergleich.</returns>
    /// <exception cref="ArgumentNullException">Wenn die Sammlung fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn eine Quelle eine Null-Migration liefert.</exception>
    /// <exception cref="InvalidOperationException">Wenn Owner und Version mehrfach vorkommen.</exception>
    public static IReadOnlyList<Migration> Order(IEnumerable<Migration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var materialized = migrations.ToArray();
        if (materialized.Any(migration => migration is null))
        {
            throw new ArgumentException("A migration source returned a null migration.", nameof(migrations));
        }

        // Die Vorprüfung verhindert, dass ein späterer Datenbankzustand von einer uneindeutigen Quelle abhängt.
        var duplicate = materialized
            .GroupBy(migration => new MigrationKey(migration.Owner, migration.Version))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            var identities = string.Join(", ", duplicate.Select(migration => migration.Identity.ToString()));
            throw new InvalidOperationException(
                $"Migration owner/version '{duplicate.Key.Owner}:{duplicate.Key.Version}' is registered more than once: {identities}.");
        }

        return materialized
            .OrderBy(migration => migration.Owner, StringComparer.Ordinal)
            .ThenBy(migration => migration.Version)
            .ThenBy(migration => migration.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private readonly record struct MigrationKey(string Owner, long Version);
}
