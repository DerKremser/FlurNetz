using Dapper;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Economy.IntegrationTests;

/// <summary>
/// Prüft Migration, Use Cases, Persistence-Adapter und PostgreSQL-Konkurrenzschutz.
/// </summary>
public sealed class EconomyPostgreSqlIntegrationTests(EconomyPostgreSqlFixture database)
    : IClassFixture<EconomyPostgreSqlFixture>
{
    [Fact]
    public async Task EconomyMigrationCreatesExactTableWithoutIdentityForeignKeyAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetEconomyMigrationAsync(factory);

        var migrationSource = new EconomyMigrationSource();
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
                WHERE table_schema = 'public' AND table_name = 'community_economies'
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
                WHERE constraint_row.conrelid = 'community_economies'::regclass
                  AND constraint_row.contype = 'p';
                """,
                cancellationToken: TestToken));
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'community_economies'::regclass AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var checkConstraint = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                """
                SELECT pg_get_constraintdef(oid)
                FROM pg_constraint
                WHERE conrelid = 'community_economies'::regclass
                  AND contype = 'c'
                  AND pg_get_constraintdef(oid) LIKE '%balance%>=%0%';
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Economy' AND version = 1;
                """,
                cancellationToken: TestToken));

        var migration = Assert.Single(migrationSource.GetMigrations());
        Assert.Equal("Economy", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateCommunityEconomies", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        Assert.Equal(["community_identity_id", "balance"], columns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(["uuid", "bigint"], columns.Select(column => column.DataType).ToArray());
        Assert.All(columns, column => Assert.Equal("NO", column.IsNullable));
        Assert.Equal("community_identity_id", primaryKey);
        Assert.Equal(0, foreignKeyCount);
        Assert.Contains("balance", checkConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">= 0", checkConstraint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstCreditLazilyCreatesEconomyWithoutIdentityTable()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateCreditUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            Assert.Equal(
                0,
                await connection.QuerySingleAsync<int>(
                    new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken)));
        }

        var result = await useCase.ExecuteAsync(communityIdentityId, 5, TestToken);

        await using var verificationConnection = await factory.OpenConnectionAsync(TestToken);
        var row = await verificationConnection.QuerySingleAsync<EconomyRow>(
            new CommandDefinition(
                """
                SELECT community_identity_id AS CommunityIdentityId,
                       balance AS Balance
                FROM community_economies;
                """,
                cancellationToken: TestToken));

        Assert.Equal(5, result.Value);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal(5, row.Balance);
    }

    [Fact]
    public async Task SubsequentCreditsAndLoadReturnTheAccumulatedDomainState()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var store = new CommunityEconomyStore(factory);
        var useCase = new CreditEconomyBalance(store);
        var communityIdentityId = CommunityIdentityId.New();

        var firstResult = await useCase.ExecuteAsync(communityIdentityId, 5, TestToken);
        var secondResult = await useCase.ExecuteAsync(communityIdentityId, 7, TestToken);
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(5, firstResult.Value);
        Assert.Equal(12, secondResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(communityIdentityId, loaded!.CommunityIdentityId);
        Assert.Equal(12, loaded.Balance.Value);
    }

    [Fact]
    public async Task StoreReturnsNullForAnUnknownValidIdentityWithoutWritingAZeroRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var store = new CommunityEconomyStore(factory);

        var loaded = await store.GetByCommunityIdentityIdAsync(CommunityIdentityId.New(), TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken));

        Assert.Null(loaded);
        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task InvalidFirstCreditLeavesNoLazyEconomyRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateCreditUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, 0, TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, -1, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task ExistingDebitPersistsTheReducedBalance()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var credit = CreateCreditUseCase(factory);
        var debit = CreateDebitUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await credit.ExecuteAsync(communityIdentityId, 10, TestToken);
        var result = await debit.ExecuteAsync(communityIdentityId, 3, TestToken);
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(7, result.Value);
        Assert.NotNull(loaded);
        Assert.Equal(7, loaded!.Balance.Value);
    }

    [Fact]
    public async Task ExactDebitLeavesTheEconomyRowAtZero()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var credit = CreateCreditUseCase(factory);
        var debit = CreateDebitUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await credit.ExecuteAsync(communityIdentityId, 10, TestToken);
        var result = await debit.ExecuteAsync(communityIdentityId, 10, TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<EconomyRow>(
            new CommandDefinition(
                "SELECT community_identity_id AS CommunityIdentityId, balance AS Balance FROM community_economies;",
                cancellationToken: TestToken));

        Assert.Equal(0, result.Value);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal(0, row.Balance);
    }

    [Fact]
    public async Task DebitOnMissingEconomyRejectsWithoutCreatingARow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateDebitUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await Assert.ThrowsAsync<InsufficientEconomyBalanceException>(
            () => useCase.ExecuteAsync(communityIdentityId, 1, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task InsufficientDebitRollsBackAndPreservesTheExistingBalance()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var credit = CreateCreditUseCase(factory);
        var debit = CreateDebitUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await credit.ExecuteAsync(communityIdentityId, 5, TestToken);
        await Assert.ThrowsAsync<InsufficientEconomyBalanceException>(
            () => debit.ExecuteAsync(communityIdentityId, 6, TestToken));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded!.Balance.Value);
    }

    [Fact]
    public async Task CreditOverflowRollsBackAndPreservesTheMaximumValue()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateCreditUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var firstResult = await useCase.ExecuteAsync(communityIdentityId, long.MaxValue, TestToken);
        await Assert.ThrowsAsync<OverflowException>(
            () => useCase.ExecuteAsync(communityIdentityId, 1, TestToken));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(long.MaxValue, firstResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(long.MaxValue, loaded!.Balance.Value);
    }

    [Fact]
    public async Task DatabaseCheckRejectsNegativeBalance()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO community_economies (community_identity_id, balance)
                VALUES (@CommunityIdentityId, @Balance);
                """,
                new
                {
                    CommunityIdentityId = communityIdentityId.Value,
                    Balance = -1L
                },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task TwentyConcurrentFirstCreditsProduceExactlyTwentyBalanceUnits()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateCreditUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => useCase.ExecuteAsync(communityIdentityId, 1, TestToken)));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(20, results.Length);
        Assert.NotNull(loaded);
        Assert.Equal(20, loaded!.Balance.Value);
    }

    [Fact]
    public async Task TwentyConcurrentCreditsOnAnExistingRowProduceTheExactAccumulatedValue()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var useCase = CreateCreditUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await useCase.ExecuteAsync(communityIdentityId, 10, TestToken);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => useCase.ExecuteAsync(communityIdentityId, 1, TestToken)));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(20, results.Length);
        Assert.NotNull(loaded);
        Assert.Equal(30, loaded!.Balance.Value);
    }

    [Fact]
    public async Task TwentyConcurrentDebitsFromTwentyUnitsAllSucceedAndReachZero()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var credit = CreateCreditUseCase(factory);
        var debit = CreateDebitUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await credit.ExecuteAsync(communityIdentityId, 20, TestToken);
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => ExecuteDebitAndClassifyAsync(debit, communityIdentityId)));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.All(outcomes, outcome => Assert.Equal(DebitOutcome.Success, outcome));
        Assert.NotNull(loaded);
        Assert.Equal(0, loaded!.Balance.Value);
    }

    [Fact]
    public async Task TwentyConcurrentDebitsAgainstTenUnitsHaveExactlyTenSuccessfulDebits()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEconomyAsync(factory);
        var credit = CreateCreditUseCase(factory);
        var debit = CreateDebitUseCase(factory);
        var store = new CommunityEconomyStore(factory);
        var communityIdentityId = CommunityIdentityId.New();

        await credit.ExecuteAsync(communityIdentityId, 10, TestToken);
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => ExecuteDebitAndClassifyAsync(debit, communityIdentityId)));
        var loaded = await store.GetByCommunityIdentityIdAsync(communityIdentityId, TestToken);

        Assert.Equal(10, outcomes.Count(outcome => outcome == DebitOutcome.Success));
        Assert.Equal(10, outcomes.Count(outcome => outcome == DebitOutcome.Insufficient));
        Assert.NotNull(loaded);
        Assert.Equal(0, loaded!.Balance.Value);
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private CreditEconomyBalance CreateCreditUseCase(PostgreSqlConnectionFactory factory) =>
        new(new CommunityEconomyStore(factory));

    private DebitEconomyBalance CreateDebitUseCase(PostgreSqlConnectionFactory factory) =>
        new(new CommunityEconomyStore(factory));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareEconomyAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetEconomyMigrationAsync(factory);
        await new MigrationRunner(factory, new EconomyMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetEconomyMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new EconomyMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_economies;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Economy' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private static async Task<DebitOutcome> ExecuteDebitAndClassifyAsync(
        DebitEconomyBalance useCase,
        CommunityIdentityId communityIdentityId)
    {
        try
        {
            await useCase.ExecuteAsync(communityIdentityId, 1, TestToken);
            return DebitOutcome.Success;
        }
        catch (InsufficientEconomyBalanceException)
        {
            return DebitOutcome.Insufficient;
        }
    }

    private enum DebitOutcome
    {
        Success,
        Insufficient
    }

    private sealed class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class EconomyRow
    {
        public Guid CommunityIdentityId { get; set; }

        public long Balance { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
