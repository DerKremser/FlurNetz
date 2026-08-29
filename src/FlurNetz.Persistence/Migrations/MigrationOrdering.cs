namespace FlurNetz.Persistence.Migrations;

public static class MigrationOrdering
{
    public static IReadOnlyList<Migration> Order(IEnumerable<Migration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var materialized = migrations.ToArray();
        if (materialized.Any(migration => migration is null))
        {
            throw new ArgumentException("A migration source returned a null migration.", nameof(migrations));
        }

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
