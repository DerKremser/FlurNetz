using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Progression.Domain;
using FlurNetz.Modules.Progression.Migrations;
using FlurNetz.Modules.Progression.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Progression.IntegrationTests;

/// <summary>
/// Prüft Migration, Use Case, Persistence-Adapter und PostgreSQL-Konkurrenzschutz.
/// </summary>
public sealed class ProgressionPostgreSqlIntegrationTests(ProgressionPostgreSqlFixture database)
    : IClassFixture<ProgressionPostgreSqlFixture>
{
    [Fact]
    public async Task ProgressionMigrationCreatesExactTableWithoutIdentityForeignKeyAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetProgressionMigrationAsync(factory);

        var migrationSource = new ProgressionMigrationSource();
        var runner = new MigrationRunner(factory, migrationSource);
        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var columns = (await connection.QueryAsync<ColumnInfo>(
            new CommandDefinition(
                """
                SELECT column_name AS ColumnName, data_type AS DataType, is_nullable AS IsNullable
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'community_progressions'
                ORDER BY ordinal_position;
                """,
                cancellationToken: TestToken))).ToArray();
        var primaryKey = await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT string_agg(attribute.attname, ',' ORDER BY key_column.ordinality)
                FROM pg_constraint constraint_row
                CROSS JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY AS key_column(attnum, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid = constraint_row.conrelid
                 AND attribute.attnum = key_column.attnum
                WHERE constraint_row.conrelid = 'community_progressions'::regclass
                  AND constraint_row.contype = 'p';
                """,
                cancellationToken: TestToken));
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'community_progressions'::regclass AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var checkConstraint = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                """
                SELECT pg_get_constraintdef(oid)
                FROM pg_constraint
                WHERE conrelid = 'community_progressions'::regclass
                  AND contype = 'c'
                  AND pg_get_constraintdef(oid) LIKE '%experience_points%>=%0%';
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Progression' AND version = 1;
                """,
                cancellationToken: TestToken));

        var migration = Assert.Single(migrationSource.GetMigrations());
        Assert.Equal("Progression", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateCommunityProgressions", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        Assert.Equal(["community_identity_id", "experience_points"], columns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(["uuid", "bigint"], columns.Select(column => column.DataType).ToArray());
        Assert.All(columns, column => Assert.Equal("NO", column.IsNullable));
        Assert.Equal("community_identity_id", primaryKey);
        Assert.Equal(0, foreignKeyCount);
        Assert.Contains("experience_points", checkConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">= 0", checkConstraint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstGrantLazilyCreatesProgressionWithoutIdentityTable()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            Assert.Equal(
                0,
                await connection.QuerySingleAsync<int>(
                    new CommandDefinition("SELECT COUNT(*) FROM community_progressions;", cancellationToken: TestToken)));
        }

        var result = await useCase.ExecuteAsync(communityIdentityId, 5, TestToken);

        await using var verificationConnection = await factory.OpenConnectionAsync(TestToken);
        var row = await verificationConnection.QuerySingleAsync<ProgressionRow>(
            new CommandDefinition(
                """
                SELECT community_identity_id AS CommunityIdentityId,
                       experience_points AS ExperiencePoints
                FROM community_progressions;
                """,
                cancellationToken: TestToken));

        Assert.Equal(5, result.Value);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal(5, row.ExperiencePoints);
    }

    [Fact]
    public async Task SubsequentGrantAndLoadReturnTheAccumulatedDomainState()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var store = new CommunityProgressionStore(factory);
        var useCase = new GrantExperience(store);
        var communityIdentityId = CommunityIdentityId.New();

        var firstResult = await useCase.ExecuteAsync(communityIdentityId, 5, TestToken);
        var secondResult = await useCase.ExecuteAsync(communityIdentityId, 7, TestToken);
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(5, firstResult.Value);
        Assert.Equal(12, secondResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(communityIdentityId, loaded!.CommunityIdentityId);
        Assert.Equal(12, loaded.ExperiencePoints.Value);
    }

    [Fact]
    public async Task StoreReturnsNullForAnUnknownValidIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var store = new CommunityProgressionStore(factory);

        var loaded = await store.GetByCommunityIdentityIdAsync(CommunityIdentityId.New(), TestToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task InvalidFirstGrantLeavesNoLazyProgressionRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, 0, TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, -1, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_progressions;", cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task OverflowRollsBackTheSecondGrantAndPreservesTheMaximumValue()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var firstResult = await useCase.ExecuteAsync(communityIdentityId, long.MaxValue, TestToken);
        await Assert.ThrowsAsync<OverflowException>(
            () => useCase.ExecuteAsync(communityIdentityId, 1, TestToken));
        var loaded = await new CommunityProgressionStore(factory)
            .GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(long.MaxValue, firstResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(long.MaxValue, loaded!.ExperiencePoints.Value);
    }

    [Fact]
    public async Task DatabaseCheckRejectsNegativeExperiencePoints()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO community_progressions (community_identity_id, experience_points)
                VALUES (@CommunityIdentityId, @ExperiencePoints);
                """,
                new
                {
                    CommunityIdentityId = communityIdentityId.Value,
                    ExperiencePoints = -1L
                },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task CancellationBeforeGrantLeavesNoProgressionRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(communityIdentityId, 5, cancellationSource.Token));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_progressions;", cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task TwentyConcurrentFirstGrantsProduceExactlyTwentyExperiencePoints()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => useCase.ExecuteAsync(communityIdentityId, 1, TestToken)));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<ProgressionRow>(
            new CommandDefinition(
                """
                SELECT community_identity_id AS CommunityIdentityId,
                       experience_points AS ExperiencePoints
                FROM community_progressions;
                """,
                cancellationToken: TestToken));
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_progressions;", cancellationToken: TestToken));

        Assert.Equal(20, results.Length);
        Assert.Equal(1, rowCount);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal(20, row.ExperiencePoints);
    }

    [Fact]
    public async Task TwentyConcurrentGrantsOnAnExistingRowProduceTheExactAccumulatedValue()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareProgressionAsync(factory);
        var useCase = CreateUseCase(factory);
        var store = new CommunityProgressionStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await useCase.ExecuteAsync(communityIdentityId, 10, TestToken);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => useCase.ExecuteAsync(communityIdentityId, 1, TestToken)));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(20, results.Length);
        Assert.NotNull(loaded);
        Assert.Equal(30, loaded!.ExperiencePoints.Value);
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private GrantExperience CreateUseCase(PostgreSqlConnectionFactory factory) =>
        new(new CommunityProgressionStore(factory));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareProgressionAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetProgressionMigrationAsync(factory);
        await new MigrationRunner(factory, new ProgressionMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetProgressionMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new ProgressionMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_progressions;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Progression' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private sealed class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class ProgressionRow
    {
        public Guid CommunityIdentityId { get; set; }

        public long ExperiencePoints { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
