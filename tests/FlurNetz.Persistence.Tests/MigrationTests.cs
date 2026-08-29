using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Persistence.Tests;

public sealed class MigrationTests
{
    [Fact]
    public void MigrationIdentityRequiresOwnerVersionAndName()
    {
        Assert.Throws<ArgumentException>(() => new MigrationIdentity("", 1, "CreateTable"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MigrationIdentity("Persistence", 0, "CreateTable"));
        Assert.Throws<ArgumentException>(() => new MigrationIdentity("Persistence", 1, ""));
    }

    [Fact]
    public void MigrationRequiresSql()
    {
        Assert.Throws<ArgumentException>(() => new Migration("Persistence", 1, "CreateTable", " "));
    }

    [Fact]
    public void MigrationIdentityHasStableValueEquality()
    {
        var first = new MigrationIdentity("Persistence", 1, "CreateHistory");
        var second = new MigrationIdentity("Persistence", 1, "CreateHistory");

        Assert.Equal(first, second);
        Assert.Equal("Persistence:1:CreateHistory", first.ToString());
    }

    [Fact]
    public void MigrationOrderingUsesOwnerVersionAndName()
    {
        var migrations = new[]
        {
            new Migration("Zeta", 1, "LaterOwner", "SELECT 1;"),
            new Migration("Alpha", 2, "Second", "SELECT 2;"),
            new Migration("Alpha", 1, "First", "SELECT 1;")
        };

        var ordered = MigrationOrdering.Order(migrations);

        Assert.Equal(
            ["Alpha:1:First", "Alpha:2:Second", "Zeta:1:LaterOwner"],
            ordered.Select(migration => migration.Identity.ToString()));
    }

    [Fact]
    public void MigrationOrderingRejectsDuplicateOwnerAndVersion()
    {
        var migrations = new[]
        {
            new Migration("Persistence", 1, "First", "SELECT 1;"),
            new Migration("Persistence", 1, "DifferentName", "SELECT 2;")
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MigrationOrdering.Order(migrations));

        Assert.Contains("Persistence:1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationChecksumIsStableAndChangesWithSql()
    {
        var checksum = MigrationChecksum.Compute("SELECT 1;");

        Assert.Equal(checksum, MigrationChecksum.Compute("SELECT 1;"));
        Assert.NotEqual(checksum, MigrationChecksum.Compute("SELECT 2;"));
        Assert.Equal(64, checksum.Length);
        Assert.All(checksum, character => Assert.True(Uri.IsHexDigit(character)));
    }

    [Fact]
    public void MigrationSourceReturnsItsMigrations()
    {
        var migration = new Migration("Persistence", 1, "CreateHistory", "SELECT 1;");
        var source = new MigrationSource([migration]);

        Assert.Same(migration, Assert.Single(source.GetMigrations()));
    }
}
