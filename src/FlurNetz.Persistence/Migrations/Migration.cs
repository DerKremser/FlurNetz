namespace FlurNetz.Persistence.Migrations;

public sealed class Migration
{
    public Migration(string owner, long version, string name, string sql)
        : this(new MigrationIdentity(owner, version, name), sql)
    {
    }

    public Migration(MigrationIdentity identity, string sql)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("Migration SQL is required.", nameof(sql));
        }

        Identity = identity;
        Sql = sql;
    }

    public MigrationIdentity Identity { get; }

    public string Owner => Identity.Owner;

    public long Version => Identity.Version;

    public string Name => Identity.Name;

    public string Sql { get; }
}
