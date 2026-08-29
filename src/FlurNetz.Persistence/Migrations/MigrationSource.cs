namespace FlurNetz.Persistence.Migrations;

public sealed class MigrationSource : IMigrationSource
{
    private readonly IReadOnlyList<Migration> migrations;

    public MigrationSource(IEnumerable<Migration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        this.migrations = migrations.ToArray();
    }

    public IEnumerable<Migration> GetMigrations() => migrations;
}
