using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using FlurNetz.Modules.Titles.Migrations;
using FlurNetz.Modules.Titles.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Titles.IntegrationTests;

/// <summary>
/// Prüft Migration, Domain-Rehydration, atomare Use Cases, DB-Invarianten und Nebenläufigkeit.
/// </summary>
public sealed class TitlesPostgreSqlIntegrationTests(TitlesPostgreSqlFixture database)
    : IClassFixture<TitlesPostgreSqlFixture>
{
    [Fact]
    public async Task TitlesMigrationCreatesExactlyThreeTablesWithInternalForeignKeysAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetTitlesMigrationAsync(factory);

        var migrationSource = new TitlesMigrationSource();
        var runner = new MigrationRunner(factory, migrationSource);
        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tableNames = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name LIKE 'community_title%'
                    ORDER BY table_name;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();

        var rootColumns = await ReadColumnsAsync(connection, "community_titles");
        var unlockColumns = await ReadColumnsAsync(connection, "community_title_unlocks");
        var selectionColumns = await ReadColumnsAsync(connection, "community_title_selections");
        var foreignKeys = (await connection.QueryAsync<ForeignKeyInfo>(
                new CommandDefinition(
                    """
                    SELECT constraint_row.conname AS ConstraintName,
                           source_table.relname AS TableName,
                           target_table.relname AS ReferencedTable,
                           pg_get_constraintdef(constraint_row.oid) AS Definition
                    FROM pg_constraint constraint_row
                    JOIN pg_class source_table ON source_table.oid = constraint_row.conrelid
                    JOIN pg_class target_table ON target_table.oid = constraint_row.confrelid
                    WHERE constraint_row.contype = 'f'
                      AND source_table.relnamespace = 'public'::regnamespace
                      AND source_table.relname LIKE 'community_title%'
                    ORDER BY constraint_row.conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var migration = Assert.Single(migrationSource.GetMigrations());
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Titles' AND version = 1;
                """,
                cancellationToken: TestToken));

        Assert.Equal(
            ["community_title_selections", "community_title_unlocks", "community_titles"],
            tableNames);
        Assert.Equal(["community_identity_id"], rootColumns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["community_identity_id", "title_definition_id"],
            unlockColumns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["community_identity_id", "title_definition_id"],
            selectionColumns.Select(column => column.ColumnName).ToArray());
        Assert.All(
            rootColumns.Concat(unlockColumns).Concat(selectionColumns),
            column =>
            {
                Assert.Equal("uuid", column.DataType);
                Assert.Equal("NO", column.IsNullable);
            });
        Assert.Equal("community_identity_id", await ReadPrimaryKeyAsync(connection, "community_titles"));
        Assert.Equal(
            "community_identity_id,title_definition_id",
            await ReadPrimaryKeyAsync(connection, "community_title_unlocks"));
        Assert.Equal("community_identity_id", await ReadPrimaryKeyAsync(connection, "community_title_selections"));
        Assert.Equal(3, foreignKeys.Length);
        Assert.Equal(
            [
                "fk_community_title_selections_community_titles",
                "fk_community_title_selections_unlock",
                "fk_community_title_unlocks_community_titles"
            ],
            foreignKeys.Select(key => key.ConstraintName).ToArray());
        Assert.All(foreignKeys, key =>
            Assert.Contains(
                key.ReferencedTable,
                new[] { "community_titles", "community_title_unlocks" }));
        Assert.DoesNotContain(foreignKeys, key => key.ReferencedTable == "community_identities");
        Assert.Equal(
            "community_title_unlocks",
            foreignKeys.Single(key => key.ConstraintName == "fk_community_title_selections_unlock").ReferencedTable);
        Assert.Equal("Titles", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateCommunityTitles", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
    }

    [Fact]
    public async Task FirstUnlockCreatesRootAndUnlockButNotSelection()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var titleDefinitionId = TitleDefinitionId.New();

        var changed = await CreateUnlock(factory).ExecuteAsync(
            communityIdentityId,
            titleDefinitionId,
            TestToken);

        Assert.True(changed);
        Assert.Equal(1, await ReadRootCountAsync(factory, communityIdentityId));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, titleDefinitionId));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task DuplicateUnlockIsAnIdempotentNoOpWithoutDuplicateRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var useCase = CreateUnlock(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var titleDefinitionId = TitleDefinitionId.New();

        Assert.True(await useCase.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken));
        Assert.False(await useCase.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken));

        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, titleDefinitionId));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task DifferentTitlesAndCommunitiesRemainIndependent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var useCase = CreateUnlock(factory);
        var firstCommunity = CommunityIdentityId.New();
        var secondCommunity = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();

        Assert.True(await useCase.ExecuteAsync(firstCommunity, firstTitle, TestToken));
        Assert.True(await useCase.ExecuteAsync(firstCommunity, secondTitle, TestToken));
        Assert.True(await useCase.ExecuteAsync(secondCommunity, firstTitle, TestToken));

        Assert.Equal(
            new[] { firstTitle.Value, secondTitle.Value }.OrderBy(value => value).ToArray(),
            (await ReadUnlocksAsync(factory, firstCommunity)).OrderBy(value => value).ToArray());
        Assert.Equal([firstTitle.Value], (await ReadUnlocksAsync(factory, secondCommunity)).ToArray());
    }

    [Fact]
    public async Task UnlockedTitleCanBecomeCurrentAndSelectionIsReplacedAtomically()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var unlock = CreateUnlock(factory);
        var setCurrent = CreateSetCurrent(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();

        await unlock.ExecuteAsync(communityIdentityId, firstTitle, TestToken);
        await unlock.ExecuteAsync(communityIdentityId, secondTitle, TestToken);

        Assert.True(await setCurrent.ExecuteAsync(communityIdentityId, firstTitle, TestToken));
        Assert.False(await setCurrent.ExecuteAsync(communityIdentityId, firstTitle, TestToken));
        Assert.True(await setCurrent.ExecuteAsync(communityIdentityId, secondTitle, TestToken));

        Assert.Equal(secondTitle.Value, await ReadCurrentAsync(factory, communityIdentityId));
        Assert.Equal(1, await ReadSelectionCountAsync(factory, communityIdentityId));
        Assert.Equal(2, await ReadUnlockCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task SelectingLockedTitleRollsBackAndPreservesExistingState()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var unlock = CreateUnlock(factory);
        var setCurrent = CreateSetCurrent(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var currentTitle = TitleDefinitionId.New();
        var lockedTitle = TitleDefinitionId.New();

        await unlock.ExecuteAsync(communityIdentityId, currentTitle, TestToken);
        await setCurrent.ExecuteAsync(communityIdentityId, currentTitle, TestToken);

        await Assert.ThrowsAsync<TitleNotUnlockedException>(() => setCurrent.ExecuteAsync(
            communityIdentityId,
            lockedTitle,
            TestToken));

        Assert.Equal([currentTitle.Value], (await ReadUnlocksAsync(factory, communityIdentityId)).ToArray());
        Assert.Equal(currentTitle.Value, await ReadCurrentAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId, lockedTitle));
    }

    [Fact]
    public async Task FailedOperationOnUnknownCommunityRollsBackTheLazyRootAnchor()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var store = new CommunityTitlesStore(factory);

        await Assert.ThrowsAsync<TitleNotUnlockedException>(() => store.ExecuteAsync(
            communityIdentityId,
            titles => titles.SetCurrent(TitleDefinitionId.New()),
            TestToken));

        Assert.Equal(0, await ReadRootCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task ClearCurrentRemovesSelectionAndKeepsUnlock()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var unlock = CreateUnlock(factory);
        var setCurrent = CreateSetCurrent(factory);
        var clearCurrent = CreateClearCurrent(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var titleDefinitionId = TitleDefinitionId.New();

        await unlock.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken);
        await setCurrent.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken);

        Assert.True(await clearCurrent.ExecuteAsync(communityIdentityId, TestToken));
        Assert.False(await clearCurrent.ExecuteAsync(communityIdentityId, TestToken));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, titleDefinitionId));
    }

    [Fact]
    public async Task LockingNonCurrentTitleKeepsCurrentAndLockingCurrentRemovesBothRows()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var unlock = CreateUnlock(factory);
        var setCurrent = CreateSetCurrent(factory);
        var lockTitle = CreateLock(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var currentTitle = TitleDefinitionId.New();
        var otherTitle = TitleDefinitionId.New();

        await unlock.ExecuteAsync(communityIdentityId, currentTitle, TestToken);
        await unlock.ExecuteAsync(communityIdentityId, otherTitle, TestToken);
        await setCurrent.ExecuteAsync(communityIdentityId, currentTitle, TestToken);

        Assert.True(await lockTitle.ExecuteAsync(communityIdentityId, otherTitle, TestToken));
        Assert.Equal(currentTitle.Value, await ReadCurrentAsync(factory, communityIdentityId));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, currentTitle));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId, otherTitle));

        Assert.True(await lockTitle.ExecuteAsync(communityIdentityId, currentTitle, TestToken));
        Assert.False(await lockTitle.ExecuteAsync(communityIdentityId, currentTitle, TestToken));
        Assert.Null(await ReadCurrentAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId, currentTitle));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task LockOnUnknownCommunityCommitsOnlyThePermittedRootAnchor()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var changed = await CreateLock(factory).ExecuteAsync(
            communityIdentityId,
            TitleDefinitionId.New(),
            TestToken);

        Assert.False(changed);
        Assert.Equal(1, await ReadRootCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task ANewStoreRehydratesUnlocksAndCurrentSelection()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var currentTitle = TitleDefinitionId.New();

        await CreateUnlock(factory).ExecuteAsync(communityIdentityId, firstTitle, TestToken);
        await CreateUnlock(factory).ExecuteAsync(communityIdentityId, currentTitle, TestToken);
        await CreateSetCurrent(factory).ExecuteAsync(communityIdentityId, currentTitle, TestToken);

        var newStore = new CommunityTitlesStore(factory);
        var result = await newStore.ExecuteAsync(
            communityIdentityId,
            titles =>
            {
                Assert.True(titles.IsUnlocked(firstTitle));
                Assert.True(titles.IsUnlocked(currentTitle));
                Assert.Equal(currentTitle, titles.CurrentTitleDefinitionId);
                return titles.SetCurrent(currentTitle);
            },
            TestToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DatabaseConstraintsRejectDuplicateUnlockAndInvalidOrSecondSelection()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO community_titles (community_identity_id) VALUES (@CommunityIdentityId);",
            new { CommunityIdentityId = communityIdentityId.Value },
            cancellationToken: TestToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO community_title_unlocks
                (community_identity_id, title_definition_id)
            VALUES
                (@CommunityIdentityId, @TitleDefinitionId);
            """,
            new
            {
                CommunityIdentityId = communityIdentityId.Value,
                TitleDefinitionId = firstTitle.Value
            },
            cancellationToken: TestToken));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO community_title_unlocks
                (community_identity_id, title_definition_id)
            VALUES
                (@CommunityIdentityId, @TitleDefinitionId);
            """,
            new
            {
                CommunityIdentityId = communityIdentityId.Value,
                TitleDefinitionId = firstTitle.Value
            },
            cancellationToken: TestToken)));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO community_title_selections
                (community_identity_id, title_definition_id)
            VALUES
                (@CommunityIdentityId, @TitleDefinitionId);
            """,
            new
            {
                CommunityIdentityId = communityIdentityId.Value,
                TitleDefinitionId = secondTitle.Value
            },
            cancellationToken: TestToken)));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO community_title_selections
                (community_identity_id, title_definition_id)
            VALUES
                (@CommunityIdentityId, @TitleDefinitionId);
            """,
            new
            {
                CommunityIdentityId = communityIdentityId.Value,
                TitleDefinitionId = firstTitle.Value
            },
            cancellationToken: TestToken));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO community_title_selections
                (community_identity_id, title_definition_id)
            VALUES
                (@CommunityIdentityId, @TitleDefinitionId);
            """,
            new
            {
                CommunityIdentityId = communityIdentityId.Value,
                TitleDefinitionId = firstTitle.Value
            },
            cancellationToken: TestToken)));
    }

    [Fact]
    public async Task ConcurrentDuplicateUnlocksYieldOneSuccessAndOneNoOp()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var titleDefinitionId = TitleDefinitionId.New();
        var firstUseCase = CreateUnlock(factory);
        var secondUseCase = CreateUnlock(factory);

        var results = await Task.WhenAll(
            firstUseCase.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken),
            secondUseCase.ExecuteAsync(communityIdentityId, titleDefinitionId, TestToken));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(1, results.Count(result => !result));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, titleDefinitionId));
    }

    [Fact]
    public async Task ConcurrentDifferentUnlocksPreserveBothChanges()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();

        await Task.WhenAll(
            CreateUnlock(factory).ExecuteAsync(communityIdentityId, firstTitle, TestToken),
            CreateUnlock(factory).ExecuteAsync(communityIdentityId, secondTitle, TestToken));

        Assert.Equal(
            new[] { firstTitle.Value, secondTitle.Value }.OrderBy(value => value).ToArray(),
            (await ReadUnlocksAsync(factory, communityIdentityId)).OrderBy(value => value).ToArray());
    }

    [Fact]
    public async Task ConcurrentCurrentChangesLeaveExactlyOneUnlockedSelection()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();
        var unlock = CreateUnlock(factory);
        await unlock.ExecuteAsync(communityIdentityId, firstTitle, TestToken);
        await unlock.ExecuteAsync(communityIdentityId, secondTitle, TestToken);

        await Task.WhenAll(
            CreateSetCurrent(factory).ExecuteAsync(communityIdentityId, firstTitle, TestToken),
            CreateSetCurrent(factory).ExecuteAsync(communityIdentityId, secondTitle, TestToken));

        var current = await ReadCurrentAsync(factory, communityIdentityId);
        Assert.NotNull(current);
        Assert.Contains(current.Value, new[] { firstTitle.Value, secondTitle.Value });
        Assert.Equal(1, await ReadSelectionCountAsync(factory, communityIdentityId));
        Assert.Equal(
            1,
            await ReadUnlockCountAsync(
                factory,
                communityIdentityId,
                TitleDefinitionId.Create(current.Value)));
    }

    [Fact]
    public async Task ConcurrentLockAndCurrentChangePreserveCurrentToUnlockInvariant()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstTitle = TitleDefinitionId.New();
        var secondTitle = TitleDefinitionId.New();
        var unlock = CreateUnlock(factory);
        await unlock.ExecuteAsync(communityIdentityId, firstTitle, TestToken);
        await unlock.ExecuteAsync(communityIdentityId, secondTitle, TestToken);
        await CreateSetCurrent(factory).ExecuteAsync(communityIdentityId, firstTitle, TestToken);

        await Task.WhenAll(
            CreateLock(factory).ExecuteAsync(communityIdentityId, firstTitle, TestToken),
            CreateSetCurrent(factory).ExecuteAsync(communityIdentityId, secondTitle, TestToken));

        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId, firstTitle));
        Assert.Equal(1, await ReadUnlockCountAsync(factory, communityIdentityId, secondTitle));
        Assert.Equal(secondTitle.Value, await ReadCurrentAsync(factory, communityIdentityId));
        Assert.Equal(1, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static UnlockCommunityTitle CreateUnlock(PostgreSqlConnectionFactory factory) =>
        new(new CommunityTitlesStore(factory));

    private static LockCommunityTitle CreateLock(PostgreSqlConnectionFactory factory) =>
        new(new CommunityTitlesStore(factory));

    private static SetCurrentCommunityTitle CreateSetCurrent(PostgreSqlConnectionFactory factory) =>
        new(new CommunityTitlesStore(factory));

    private static ClearCurrentCommunityTitle CreateClearCurrent(PostgreSqlConnectionFactory factory) =>
        new(new CommunityTitlesStore(factory));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareTitlesAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetTitlesMigrationAsync(factory);
        await new MigrationRunner(factory, new TitlesMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetTitlesMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new TitlesMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_title_selections;
                DROP TABLE IF EXISTS community_title_unlocks;
                DROP TABLE IF EXISTS community_titles;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Titles' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private static async Task<ColumnInfo[]> ReadColumnsAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        return (await connection.QueryAsync<ColumnInfo>(
                new CommandDefinition(
                    """
                    SELECT column_name AS ColumnName,
                           data_type AS DataType,
                           is_nullable AS IsNullable
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @TableName
                    ORDER BY ordinal_position;
                    """,
                    new { TableName = tableName },
                    cancellationToken: TestToken)))
            .ToArray();
    }

    private static Task<string> ReadPrimaryKeyAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        return connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT string_agg(attribute.attname, ',' ORDER BY key_column.ordinality)
                FROM pg_constraint constraint_row
                CROSS JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY AS key_column(attnum, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid = constraint_row.conrelid
                 AND attribute.attnum = key_column.attnum
                WHERE constraint_row.conrelid = to_regclass(@TableName)
                  AND constraint_row.contype = 'p';
                """,
                new { TableName = tableName },
                cancellationToken: TestToken));
    }

    private static async Task<int> ReadRootCountAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_titles WHERE community_identity_id = @CommunityIdentityId;",
                new { CommunityIdentityId = communityIdentityId.Value },
                cancellationToken: TestToken));
    }

    private static async Task<int> ReadUnlockCountAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId,
        TitleDefinitionId? titleDefinitionId = null)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        const string sql = """
            SELECT COUNT(*)
            FROM community_title_unlocks
            WHERE community_identity_id = @CommunityIdentityId
              AND (@TitleDefinitionId IS NULL OR title_definition_id = @TitleDefinitionId);
            """;
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    CommunityIdentityId = communityIdentityId.Value,
                    TitleDefinitionId = titleDefinitionId?.Value
                },
                cancellationToken: TestToken));
    }

    private static async Task<int> ReadSelectionCountAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_title_selections WHERE community_identity_id = @CommunityIdentityId;",
                new { CommunityIdentityId = communityIdentityId.Value },
                cancellationToken: TestToken));
    }

    private static async Task<Guid?> ReadCurrentAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleOrDefaultAsync<CurrentRow>(
            new CommandDefinition(
                """
                SELECT title_definition_id AS TitleDefinitionId
                FROM community_title_selections
                WHERE community_identity_id = @CommunityIdentityId;
                """,
                new { CommunityIdentityId = communityIdentityId.Value },
                cancellationToken: TestToken));
        return row?.TitleDefinitionId;
    }

    private static async Task<Guid[]> ReadUnlocksAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId communityIdentityId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return (await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    """
                    SELECT title_definition_id
                    FROM community_title_unlocks
                    WHERE community_identity_id = @CommunityIdentityId;
                    """,
                    new { CommunityIdentityId = communityIdentityId.Value },
                    cancellationToken: TestToken)))
            .ToArray();
    }

    private sealed class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class ForeignKeyInfo
    {
        public string ConstraintName { get; set; } = string.Empty;

        public string TableName { get; set; } = string.Empty;

        public string ReferencedTable { get; set; } = string.Empty;

        public string Definition { get; set; } = string.Empty;
    }

    private sealed class CurrentRow
    {
        public Guid TitleDefinitionId { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
