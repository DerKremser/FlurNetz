using Dapper;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;
using FlurNetz.Modules.Rewards.Migrations;
using FlurNetz.Modules.Rewards.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Rewards.IntegrationTests;

/// <summary>
/// Prüft Migration, Katalog, atomare Grants und die Economy-Komposition gegen PostgreSQL.
/// </summary>
public sealed class RewardsPostgreSqlIntegrationTests(RewardsPostgreSqlFixture database)
    : IClassFixture<RewardsPostgreSqlFixture>
{
    [Fact]
    public async Task RewardsMigrationCreatesTheExpectedTablesConstraintsAndHistory()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory);

        var migrationSource = new RewardsMigrationSource();
        var runner = new MigrationRunner(factory, migrationSource);
        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var columns = (await connection.QueryAsync<ColumnInfo>(
                new CommandDefinition(
                    """
                    SELECT table_name AS TableName,
                           column_name AS ColumnName,
                           data_type AS DataType,
                           is_nullable AS IsNullable
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name IN (
                          'reward_definitions',
                          'reward_packages',
                          'reward_package_definitions',
                          'reward_grants')
                    ORDER BY table_name, ordinal_position;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();

        Assert.Equal(
            [
                new ColumnInfo("reward_definitions", "id", "uuid", "NO"),
                new ColumnInfo("reward_definitions", "definition_type", "text", "NO"),
                new ColumnInfo("reward_definitions", "amount", "bigint", "NO"),
                new ColumnInfo("reward_grants", "id", "uuid", "NO"),
                new ColumnInfo("reward_grants", "community_identity_id", "uuid", "NO"),
                new ColumnInfo("reward_grants", "reward_definition_id", "uuid", "NO"),
                new ColumnInfo("reward_grants", "source_type", "text", "NO"),
                new ColumnInfo("reward_grants", "source_id", "text", "NO"),
                new ColumnInfo("reward_package_definitions", "reward_package_id", "uuid", "NO"),
                new ColumnInfo("reward_package_definitions", "reward_definition_id", "uuid", "NO"),
                new ColumnInfo("reward_packages", "id", "uuid", "NO")
            ],
            columns);

        Assert.Equal("id", await GetPrimaryKeyColumnsAsync(connection, "reward_definitions"));
        Assert.Equal("id", await GetPrimaryKeyColumnsAsync(connection, "reward_packages"));
        Assert.Equal(
            "reward_package_id,reward_definition_id",
            await GetPrimaryKeyColumnsAsync(connection, "reward_package_definitions"));
        Assert.Equal("id", await GetPrimaryKeyColumnsAsync(connection, "reward_grants"));

        var packageForeignKeys = (await connection.QueryAsync<ForeignKeyInfo>(
                new CommandDefinition(
                    """
                    SELECT constraint_row.conname AS ConstraintName,
                           referenced_table.relname AS ReferencedTable
                    FROM pg_constraint constraint_row
                    JOIN pg_class referenced_table
                      ON referenced_table.oid = constraint_row.confrelid
                    WHERE constraint_row.conrelid = 'reward_package_definitions'::regclass
                      AND constraint_row.contype = 'f'
                    ORDER BY constraint_row.conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var grantForeignKeys = (await connection.QueryAsync<ForeignKeyInfo>(
                new CommandDefinition(
                    """
                    SELECT constraint_row.conname AS ConstraintName,
                           referenced_table.relname AS ReferencedTable
                    FROM pg_constraint constraint_row
                    JOIN pg_class referenced_table
                      ON referenced_table.oid = constraint_row.confrelid
                    WHERE constraint_row.conrelid = 'reward_grants'::regclass
                      AND constraint_row.contype = 'f'
                    ORDER BY constraint_row.conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();

        Assert.Equal(["reward_definitions", "reward_packages"],
            packageForeignKeys.Select(key => key.ReferencedTable).OrderBy(name => name).ToArray());
        Assert.Equal(["reward_definitions"],
            grantForeignKeys.Select(key => key.ReferencedTable).ToArray());
        Assert.DoesNotContain(packageForeignKeys, key =>
            key.ReferencedTable is "community_identities" or "community_economies");
        Assert.DoesNotContain(grantForeignKeys, key =>
            key.ReferencedTable is "community_identities" or "community_economies");

        Assert.Equal(
            "source_type,source_id,reward_definition_id",
            await GetUniqueConstraintColumnsAsync(connection, "reward_grants"));

        var migration = Assert.Single(migrationSource.GetMigrations());
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Rewards' AND version = 1;
                """,
                cancellationToken: TestToken));

        Assert.Equal("Rewards", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateRewardConfigurationAndGrants", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
    }

    [Fact]
    public async Task RewardsAndEconomyMigrationsCanBeRunTogetherAndTwice()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory);

        var runner = new MigrationRunner(
            factory,
            [new EconomyMigrationSource(), new RewardsMigrationSource()]);

        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(2, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 2), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var historyRows = await connection.QueryAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Rewards' AND version = 1;
                """,
                cancellationToken: TestToken));

        var history = Assert.Single(historyRows);
        Assert.Equal(MigrationChecksum.Compute(Assert.Single(new RewardsMigrationSource().GetMigrations()).Sql), history.Checksum);
    }

    [Fact]
    public async Task CreateDefinitionPersistsTheStableTypeAndAmount()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<DefinitionRow>(
            new CommandDefinition(
                "SELECT id AS Id, definition_type AS DefinitionType, amount AS Amount FROM reward_definitions;",
                cancellationToken: TestToken));

        Assert.Equal(definitionId.Value, row.Id);
        Assert.Equal("economy_balance", row.DefinitionType);
        Assert.Equal(5, row.Amount);
    }

    [Fact]
    public async Task CreatePackagePersistsOnePackageAndItsMembership()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_packages;", cancellationToken: TestToken)));
        var membership = await connection.QuerySingleAsync<MembershipRow>(
            new CommandDefinition(
                """
                SELECT reward_package_id AS RewardPackageId,
                       reward_definition_id AS RewardDefinitionId
                FROM reward_package_definitions;
                """,
                cancellationToken: TestToken));

        Assert.Equal(packageId.Value, membership.RewardPackageId);
        Assert.Equal(definitionId.Value, membership.RewardDefinitionId);
    }

    [Fact]
    public async Task MultiDefinitionPackageCreditsAllDefinitionsAtomically()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionUseCase = CreateDefinitionUseCase(factory);
        var firstDefinitionId = await definitionUseCase.ExecuteAsync(3, TestToken);
        var secondDefinitionId = await definitionUseCase.ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync(
            [firstDefinitionId, secondDefinitionId],
            TestToken);
        var communityIdentityId = CommunityIdentityId.New();

        var outcome = await CreateGrantUseCase(factory).ExecuteAsync(
            packageId,
            communityIdentityId,
            RewardSource.Create("test", "multi-definition"),
            TestToken);

        Assert.Equal(RewardPackageGrantOutcome.Granted, outcome);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(2, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(8, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task FirstGrantLazilyCreatesEconomyAndPersistsOneGrant()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var communityIdentityId = CommunityIdentityId.New();

        await using (var beforeConnection = await factory.OpenConnectionAsync(TestToken))
        {
            Assert.Equal(0, await beforeConnection.QuerySingleAsync<int>(
                new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken)));
        }

        var outcome = await CreateGrantUseCase(factory).ExecuteAsync(
            packageId,
            communityIdentityId,
            RewardSource.Create("test", "grant-1"),
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(RewardPackageGrantOutcome.Granted, outcome);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(5, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DuplicateGrantIsAnIdempotentNoOp()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        var source = RewardSource.Create("test", "duplicate");
        var grant = CreateGrantUseCase(factory);

        var firstOutcome = await grant.ExecuteAsync(packageId, communityIdentityId, source, TestToken);
        var secondOutcome = await grant.ExecuteAsync(packageId, communityIdentityId, source, TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(RewardPackageGrantOutcome.Granted, firstOutcome);
        Assert.Equal(RewardPackageGrantOutcome.AlreadyGranted, secondOutcome);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(5, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DifferentSourcesGrantTheSamePackageAgain()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        var grant = CreateGrantUseCase(factory);

        var firstOutcome = await grant.ExecuteAsync(
            packageId,
            communityIdentityId,
            RewardSource.Create("test", "source-a"),
            TestToken);
        var secondOutcome = await grant.ExecuteAsync(
            packageId,
            communityIdentityId,
            RewardSource.Create("test", "source-b"),
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(RewardPackageGrantOutcome.Granted, firstOutcome);
        Assert.Equal(RewardPackageGrantOutcome.Granted, secondOutcome);
        Assert.Equal(2, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(10, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task CommunityIdentityIsNotPartOfTheGrantUniquenessBoundary()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var firstCommunityIdentityId = CommunityIdentityId.New();
        var secondCommunityIdentityId = CommunityIdentityId.New();
        var source = RewardSource.Create("test", "identity-independent");
        var grant = CreateGrantUseCase(factory);

        var firstOutcome = await grant.ExecuteAsync(
            packageId,
            firstCommunityIdentityId,
            source,
            TestToken);
        var secondOutcome = await grant.ExecuteAsync(
            packageId,
            secondCommunityIdentityId,
            source,
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(RewardPackageGrantOutcome.Granted, firstOutcome);
        Assert.Equal(RewardPackageGrantOutcome.AlreadyGranted, secondOutcome);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(5, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = firstCommunityIdentityId.Value },
                cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = secondCommunityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task ConcurrentDuplicateGrantsProduceOneEffectAndOneAlreadyGrantedOutcome()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        var source = RewardSource.Create("test", "concurrent");
        var grant = CreateGrantUseCase(factory);

        var outcomes = await Task.WhenAll(
            grant.ExecuteAsync(packageId, communityIdentityId, source, TestToken),
            grant.ExecuteAsync(packageId, communityIdentityId, source, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, outcomes.Count(outcome => outcome == RewardPackageGrantOutcome.Granted));
        Assert.Equal(1, outcomes.Count(outcome => outcome == RewardPackageGrantOutcome.AlreadyGranted));
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(5, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task PackageOverflowRollsBackEveryGrantAndEconomyEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionUseCase = CreateDefinitionUseCase(factory);
        var firstDefinitionId = await definitionUseCase.ExecuteAsync(3, TestToken);
        var secondDefinitionId = await definitionUseCase.ExecuteAsync(3, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync(
            [firstDefinitionId, secondDefinitionId],
            TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        await InsertEconomyAsync(factory, communityIdentityId, long.MaxValue - 4);

        await Assert.ThrowsAsync<OverflowException>(
            () => CreateGrantUseCase(factory).ExecuteAsync(
                packageId,
                communityIdentityId,
                RewardSource.Create("test", "package-overflow"),
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(long.MaxValue - 4, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task SingleEffectOverflowRollsBackItsGrant()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(1, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync([definitionId], TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        await InsertEconomyAsync(factory, communityIdentityId, long.MaxValue);

        await Assert.ThrowsAsync<OverflowException>(
            () => CreateGrantUseCase(factory).ExecuteAsync(
                packageId,
                communityIdentityId,
                RewardSource.Create("test", "single-overflow"),
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(long.MaxValue, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT balance FROM community_economies WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task UnknownPersistedDefinitionTypeIsRejectedBeforeAnyEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO reward_definitions (id, definition_type, amount)
                    VALUES (@DefinitionId, 'future_type', 1);
                    INSERT INTO reward_packages (id)
                    VALUES (@PackageId);
                    INSERT INTO reward_package_definitions
                        (reward_package_id, reward_definition_id)
                    VALUES (@PackageId, @DefinitionId);
                    """,
                    new { DefinitionId = definitionId, PackageId = packageId },
                    cancellationToken: TestToken));
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateGrantUseCase(factory).ExecuteAsync(
                RewardPackageId.Create(packageId),
                CommunityIdentityId.New(),
                RewardSource.Create("test", "unknown-type"),
                TestToken));

        await using var verificationConnection = await factory.OpenConnectionAsync(TestToken);
        Assert.Contains("future_type", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await verificationConnection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(0, await verificationConnection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task PartialExistingPackageGrantFailsWithoutExecutingTheMissingDefinition()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var definitionUseCase = CreateDefinitionUseCase(factory);
        var firstDefinitionId = await definitionUseCase.ExecuteAsync(3, TestToken);
        var secondDefinitionId = await definitionUseCase.ExecuteAsync(5, TestToken);
        var packageId = await CreatePackageUseCase(factory).ExecuteAsync(
            [firstDefinitionId, secondDefinitionId],
            TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        var source = RewardSource.Create("test", "partial");
        await InsertGrantAsync(factory, communityIdentityId, firstDefinitionId, source);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateGrantUseCase(factory).ExecuteAsync(
                packageId,
                communityIdentityId,
                source,
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Contains("Partial-Grant", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task UnknownPackageFailsWithoutCreatingAGrantOrEconomyRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateGrantUseCase(factory).ExecuteAsync(
                RewardPackageId.New(),
                CommunityIdentityId.New(),
                RewardSource.Create("test", "unknown-package"),
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_grants;", cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM community_economies;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task CreatingAPackageWithAMissingDefinitionLeavesNoPackageRows()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var knownDefinitionId = await CreateDefinitionUseCase(factory).ExecuteAsync(5, TestToken);
        var missingDefinitionId = RewardDefinitionId.New();

        await Assert.ThrowsAsync<RewardDefinitionNotFoundException>(
            () => CreatePackageUseCase(factory).ExecuteAsync(
                [knownDefinitionId, missingDefinitionId],
                TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_packages;", cancellationToken: TestToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM reward_package_definitions;", cancellationToken: TestToken)));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static CreateEconomyBalanceRewardDefinition CreateDefinitionUseCase(
        PostgreSqlConnectionFactory factory) =>
        new(new PostgreSqlRewardCatalogStore(factory));

    private static CreateRewardPackage CreatePackageUseCase(
        PostgreSqlConnectionFactory factory) =>
        new(new PostgreSqlRewardCatalogStore(factory));

    private static GrantRewardPackage CreateGrantUseCase(
        PostgreSqlConnectionFactory factory)
    {
        var economyStore = new CommunityEconomyStore(factory);
        var economyBalanceCredit = new EconomyBalanceCredit(economyStore);
        var executor = new PostgreSqlRewardPackageGrantExecutor(factory, economyBalanceCredit);
        return new GrantRewardPackage(executor);
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetDatabaseAsync(factory);
        await new MigrationRunner(
                factory,
                [new EconomyMigrationSource(), new RewardsMigrationSource()])
            .RunAsync(TestToken);
    }

    private static async Task ResetDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new EconomyMigrationSource()).RunAsync(TestToken);
        await new MigrationRunner(factory, new RewardsMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS reward_grants;
                DROP TABLE IF EXISTS reward_package_definitions;
                DROP TABLE IF EXISTS reward_packages;
                DROP TABLE IF EXISTS reward_definitions;
                DROP TABLE IF EXISTS community_economies;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE (owner = 'Economy' AND version = 1)
                   OR (owner = 'Rewards' AND version = 1);
                """,
                cancellationToken: TestToken));
    }

    private static async Task InsertEconomyAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId,
        long balance)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO community_economies (community_identity_id, balance)
                VALUES (@CommunityIdentityId, @Balance);
                """,
                new
                {
                    CommunityIdentityId = communityIdentityId.Value,
                    Balance = balance
                },
                cancellationToken: TestToken));
    }

    private static async Task InsertGrantAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId,
        RewardDefinitionId rewardDefinitionId,
        RewardSource source)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO reward_grants
                    (id, community_identity_id, reward_definition_id, source_type, source_id)
                VALUES
                    (@Id, @CommunityIdentityId, @RewardDefinitionId, @SourceType, @SourceId);
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    CommunityIdentityId = communityIdentityId.Value,
                    RewardDefinitionId = rewardDefinitionId.Value,
                    SourceType = source.SourceType,
                    SourceId = source.SourceId
                },
                cancellationToken: TestToken));
    }

    private static async Task<string> GetPrimaryKeyColumnsAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        return await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT string_agg(attribute.attname, ',' ORDER BY key_column.ordinality)
                FROM pg_constraint constraint_row
                CROSS JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY
                    AS key_column(attnum, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid = constraint_row.conrelid
                 AND attribute.attnum = key_column.attnum
                WHERE constraint_row.conrelid = CAST(@TableName AS regclass)
                  AND constraint_row.contype = 'p';
                """,
                new { TableName = tableName },
                cancellationToken: TestToken));
    }

    private static async Task<string> GetUniqueConstraintColumnsAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        return await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT string_agg(attribute.attname, ',' ORDER BY key_column.ordinality)
                FROM pg_constraint constraint_row
                CROSS JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY
                    AS key_column(attnum, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid = constraint_row.conrelid
                 AND attribute.attnum = key_column.attnum
                WHERE constraint_row.conrelid = CAST(@TableName AS regclass)
                  AND constraint_row.contype = 'u';
                """,
                new { TableName = tableName },
                cancellationToken: TestToken));
    }

    private sealed record ColumnInfo(
        string TableName,
        string ColumnName,
        string DataType,
        string IsNullable);

    private sealed class ForeignKeyInfo
    {
        public string ConstraintName { get; set; } = string.Empty;

        public string ReferencedTable { get; set; } = string.Empty;
    }

    private sealed class DefinitionRow
    {
        public Guid Id { get; set; }

        public string DefinitionType { get; set; } = string.Empty;

        public long Amount { get; set; }
    }

    private sealed class MembershipRow
    {
        public Guid RewardPackageId { get; set; }

        public Guid RewardDefinitionId { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
