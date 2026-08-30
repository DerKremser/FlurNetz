using Dapper;
using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Identity.IntegrationTests;

/// <summary>
/// Prüft Migration, Create-Use-Case und Persistence-Adapter des Identity-Vertical-Slices.
/// </summary>
public sealed class IdentityPostgreSqlIntegrationTests(IdentityPostgreSqlFixture database)
    : IClassFixture<IdentityPostgreSqlFixture>
{
    [Fact]
    public async Task IdentityMigrationCreatesExactTableAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetIdentityMigrationAsync(factory);

        var runner = new MigrationRunner(factory, new IdentityMigrationSource());
        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var columns = (await connection.QueryAsync<IdentityColumn>(
            new CommandDefinition(
                """
                SELECT column_name AS ColumnName, data_type AS DataType, is_nullable AS IsNullable
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'community_identities'
                ORDER BY ordinal_position;
                """,
                cancellationToken: TestToken))).ToArray();
        var primaryKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'community_identities'::regclass AND contype = 'p';
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Identity' AND version = 1;
                """,
                cancellationToken: TestToken));

        var migration = Assert.Single(new IdentityMigrationSource().GetMigrations());
        Assert.Equal("Identity", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateCommunityIdentities", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        var column = Assert.Single(columns);
        Assert.Equal("id", column.ColumnName);
        Assert.Equal("uuid", column.DataType);
        Assert.Equal("NO", column.IsNullable);
        Assert.Equal(1, primaryKeyCount);
    }

    [Fact]
    public async Task CreateUseCaseCommitsOneUuidRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var repository = new CommunityIdentityRepository(factory);
        var useCase = new CreateCommunityIdentity(repository);

        var id = await useCase.ExecuteAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_identities;", cancellationToken: TestToken));
        var storedId = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition("SELECT id FROM community_identities;", cancellationToken: TestToken));

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(1, rowCount);
        Assert.Equal(id.Value, storedId);
    }

    [Fact]
    public async Task RepositoryLoadsThePersistedDomainIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var repository = new CommunityIdentityRepository(factory);
        var id = CommunityIdentityId.New();
        var identity = CommunityIdentity.Create(id);

        await repository.AddAsync(identity, TestToken);
        var loaded = await repository.GetByIdAsync(id, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded!.Id);
    }

    [Fact]
    public async Task RepositoryReturnsNullForAnUnknownValidIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var repository = new CommunityIdentityRepository(factory);

        var loaded = await repository.GetByIdAsync(CommunityIdentityId.New(), TestToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task RepositoryRejectsDuplicatePrimaryKeyInsteadOfUpserting()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var repository = new CommunityIdentityRepository(factory);
        var identity = CommunityIdentity.Create(CommunityIdentityId.New());

        await repository.AddAsync(identity, TestToken);
        await Assert.ThrowsAnyAsync<Exception>(() => repository.AddAsync(identity, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_identities;", cancellationToken: TestToken));

        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task RepositoryWriteCanBeRolledBackWithinThePersistenceTransaction()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var repository = new CommunityIdentityRepository(factory);
        var identity = CommunityIdentity.Create(CommunityIdentityId.New());

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await repository.AddAsync(identity, transaction, TestToken);
            await transaction.RollbackAsync(TestToken);
        }

        Assert.Null(await repository.GetByIdAsync(identity.Id, TestToken));
    }

    [Fact]
    public async Task MultipleCreatesProduceDistinctIdsAndRows()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareIdentityAsync(factory);
        var useCase = new CreateCommunityIdentity(new CommunityIdentityRepository(factory));

        var ids = new[]
        {
            await useCase.ExecuteAsync(TestToken),
            await useCase.ExecuteAsync(TestToken),
            await useCase.ExecuteAsync(TestToken)
        };

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_identities;", cancellationToken: TestToken));

        Assert.Equal(3, ids.Distinct().Count());
        Assert.Equal(3, rowCount);
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareIdentityAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetIdentityMigrationAsync(factory);
        await new MigrationRunner(factory, new IdentityMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetIdentityMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new IdentityMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_identities;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Identity' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private sealed class IdentityColumn
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
