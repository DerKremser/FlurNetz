namespace FlurNetz.Persistence.Migrations;

public sealed record MigrationIdentity
{
    public MigrationIdentity(string owner, long version, string name)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("A migration owner is required.", nameof(owner));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A migration version must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A migration name is required.", nameof(name));
        }

        Owner = owner;
        Version = version;
        Name = name;
    }

    public string Owner { get; }

    public long Version { get; }

    public string Name { get; }

    public override string ToString() => $"{Owner}:{Version}:{Name}";
}
