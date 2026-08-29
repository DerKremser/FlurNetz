using Dapper;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Persistence.IntegrationTests;

public sealed class PostgreSqlIntegrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ConnectionCanOpenAndExecuteSql()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await using var connection = await factory.OpenConnectionAsync(TestCancellationToken);

        var result = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT 1;", cancellationToken: TestCancellationToken));

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task TransactionCommitPersistsChanges()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("commit");
        await using var factory = CreateFactory();

        try
        {
            await using (var setupConnection = await factory.OpenConnectionAsync(TestCancellationToken))
            {
                await setupConnection.ExecuteAsync(
                    new CommandDefinition($"CREATE TABLE {table} (id integer NOT NULL);", cancellationToken: TestCancellationToken));
            }

            await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestCancellationToken))
            {
                await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        $"INSERT INTO {table} (id) VALUES (@Id);",
                        new { Id = 42 },
                        transaction: transaction.Transaction,
                        cancellationToken: TestCancellationToken));
                await transaction.CommitAsync(TestCancellationToken);
            }

            await using var verificationConnection = await factory.OpenConnectionAsync(TestCancellationToken);
            var result = await verificationConnection.QuerySingleAsync<int>(
                new CommandDefinition($"SELECT id FROM {table};", cancellationToken: TestCancellationToken));

            Assert.Equal(42, result);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task TransactionRollbackDiscardsChanges()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("rollback");
        await using var factory = CreateFactory();

        try
        {
            await using (var setupConnection = await factory.OpenConnectionAsync(TestCancellationToken))
            {
                await setupConnection.ExecuteAsync(
                    new CommandDefinition($"CREATE TABLE {table} (id integer NOT NULL);", cancellationToken: TestCancellationToken));
            }

            await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestCancellationToken))
            {
                await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        $"INSERT INTO {table} (id) VALUES (@Id);",
                        new { Id = 42 },
                        transaction: transaction.Transaction,
                        cancellationToken: TestCancellationToken));
                await transaction.RollbackAsync(TestCancellationToken);
            }

            await using var verificationConnection = await factory.OpenConnectionAsync(TestCancellationToken);
            var count = await verificationConnection.QuerySingleAsync<int>(
                new CommandDefinition($"SELECT COUNT(*) FROM {table};", cancellationToken: TestCancellationToken));

            Assert.Equal(0, count);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task MigrationRunnerCreatesHistoryAppliesMigrationAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("migration");
        var owner = NewOwnerName();
        var migration = new Migration(
            owner,
            1,
            "CreateMigrationTable",
            $"CREATE TABLE {table} (id integer NOT NULL);");
        await using var factory = CreateFactory();
        var runner = new MigrationRunner(factory, new MigrationSource([migration]));

        try
        {
            var firstRun = await runner.RunAsync(TestCancellationToken);
            var secondRun = await runner.RunAsync(TestCancellationToken);

            Assert.Equal(new MigrationRunResult(1, 0), firstRun);
            Assert.Equal(new MigrationRunResult(0, 1), secondRun);

            await using var connection = await factory.OpenConnectionAsync(TestCancellationToken);
            var tableExists = await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    "SELECT to_regclass(@TableName) IS NOT NULL;",
                    new { TableName = table },
                    cancellationToken: TestCancellationToken));
            var historyCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(
                    $"SELECT COUNT(*) FROM {MigrationRunner.MigrationHistoryTableName} WHERE owner = @Owner AND version = @Version;",
                    new { Owner = owner, Version = 1L },
                    cancellationToken: TestCancellationToken));

            Assert.True(tableExists);
            Assert.Equal(1, historyCount);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task MigrationRunnerAppliesMigrationsInDeterministicOrder()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("order");
        var owner = NewOwnerName();
        var firstMigration = new Migration(
            owner,
            1,
            "CreateOrderTable",
            $"CREATE TABLE {table} (id integer GENERATED ALWAYS AS IDENTITY, step integer NOT NULL); INSERT INTO {table} (step) VALUES (1);");
        var secondMigration = new Migration(
            owner,
            2,
            "InsertSecondStep",
            $"INSERT INTO {table} (step) VALUES (2);");
        await using var factory = CreateFactory();
        var source = new MigrationSource([secondMigration, firstMigration]);
        var runner = new MigrationRunner(factory, source);

        try
        {
            await runner.RunAsync(TestCancellationToken);

            await using var connection = await factory.OpenConnectionAsync(TestCancellationToken);
            var steps = (await connection.QueryAsync<int>(
                new CommandDefinition($"SELECT step FROM {table} ORDER BY id;", cancellationToken: TestCancellationToken))).ToArray();

            Assert.Equal([1, 2], steps);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task FailedMigrationRollsBackAndIsNotRegistered()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("failed");
        var owner = NewOwnerName();
        var migration = new Migration(
            owner,
            1,
            "FailedMigration",
            $"CREATE TABLE {table} (id integer NOT NULL); INSERT INTO {table} (id) VALUES (1); THIS IS NOT VALID SQL;");
        await using var factory = CreateFactory();
        var runner = new MigrationRunner(factory, new MigrationSource([migration]));

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => runner.RunAsync(TestCancellationToken));

            await using var connection = await factory.OpenConnectionAsync(TestCancellationToken);
            var tableExists = await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    "SELECT to_regclass(@TableName) IS NOT NULL;",
                    new { TableName = table },
                    cancellationToken: TestCancellationToken));
            var historyCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(
                    $"SELECT COUNT(*) FROM {MigrationRunner.MigrationHistoryTableName} WHERE owner = @Owner AND version = @Version;",
                    new { Owner = owner, Version = 1L },
                    cancellationToken: TestCancellationToken));

            Assert.False(tableExists);
            Assert.Equal(0, historyCount);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    [Fact]
    public async Task ChangedAppliedMigrationFailsChecksumValidation()
    {
        SkipIfDatabaseIsUnavailable();
        var table = NewTableName("checksum");
        var owner = NewOwnerName();
        var original = new Migration(owner, 1, "CreateChecksumTable", $"CREATE TABLE {table} (id integer NOT NULL);");
        var changed = new Migration(owner, 1, "CreateChecksumTable", $"CREATE TABLE {table} (id bigint NOT NULL);");
        await using var factory = CreateFactory();

        try
        {
            await new MigrationRunner(factory, new MigrationSource([original])).RunAsync(TestCancellationToken);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new MigrationRunner(factory, new MigrationSource([changed])).RunAsync(TestCancellationToken));

            Assert.Contains("was changed", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await DropTableAsync(factory, table);
        }
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static string NewTableName(string purpose) => $"flurnetz_test_{purpose}_{Guid.NewGuid():N}";

    private static string NewOwnerName() => $"IntegrationTests_{Guid.NewGuid():N}";

    private static async Task DropTableAsync(PostgreSqlConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenConnectionAsync(TestCancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition($"DROP TABLE IF EXISTS {table};", cancellationToken: TestCancellationToken));
    }
}
