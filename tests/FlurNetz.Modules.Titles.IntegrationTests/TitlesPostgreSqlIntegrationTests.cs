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
    public async Task TitlesMigrationsCreateCommunityStateAndCatalogTablesWithConstraintsAndAreIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetTitlesMigrationAsync(factory);

        var migrationSource = new TitlesMigrationSource();
        var runner = new MigrationRunner(factory, migrationSource);
        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(2, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 2), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tableNames = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name IN (
                          'community_titles',
                          'community_title_unlocks',
                          'community_title_selections',
                          'title_definitions'
                      )
                    ORDER BY table_name;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();

        var rootColumns = await ReadColumnsAsync(connection, "community_titles");
        var unlockColumns = await ReadColumnsAsync(connection, "community_title_unlocks");
        var selectionColumns = await ReadColumnsAsync(connection, "community_title_selections");
        var definitionColumns = await ReadColumnsAsync(connection, "title_definitions");
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
                      AND source_table.relname IN (
                          'community_titles',
                          'community_title_unlocks',
                          'community_title_selections',
                          'title_definitions'
                      )
                    ORDER BY constraint_row.conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var migrations = migrationSource.GetMigrations().ToArray();
        var versionOne = migrations.Single(migration => migration.Version == 1);
        var versionTwo = migrations.Single(migration => migration.Version == 2);
        var histories = (await connection.QueryAsync<MigrationHistory>(
                new CommandDefinition(
                    $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Titles' AND version IN (1, 2)
                ORDER BY version;
                """,
                cancellationToken: TestToken)))
            .ToArray();
        var checkConstraints = await ReadCheckConstraintNamesAsync(
            connection,
            "title_definitions");

        Assert.Equal(
            [
                "community_title_selections",
                "community_title_unlocks",
                "community_titles",
                "title_definitions"
            ],
            tableNames);
        Assert.Equal(["community_identity_id"], rootColumns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["community_identity_id", "title_definition_id"],
            unlockColumns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["community_identity_id", "title_definition_id"],
            selectionColumns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["id", "display_name", "description"],
            definitionColumns.Select(column => column.ColumnName).ToArray());
        Assert.All(
            rootColumns.Concat(unlockColumns).Concat(selectionColumns),
            column =>
            {
                Assert.Equal("uuid", column.DataType);
                Assert.Equal("NO", column.IsNullable);
            });
        Assert.Equal("uuid", definitionColumns[0].DataType);
        Assert.Equal("NO", definitionColumns[0].IsNullable);
        Assert.Equal("character varying", definitionColumns[1].DataType);
        Assert.Equal(100, definitionColumns[1].CharacterMaximumLength);
        Assert.Equal("NO", definitionColumns[1].IsNullable);
        Assert.Equal("character varying", definitionColumns[2].DataType);
        Assert.Equal(500, definitionColumns[2].CharacterMaximumLength);
        Assert.Equal("YES", definitionColumns[2].IsNullable);
        Assert.Equal("community_identity_id", await ReadPrimaryKeyAsync(connection, "community_titles"));
        Assert.Equal(
            "community_identity_id,title_definition_id",
            await ReadPrimaryKeyAsync(connection, "community_title_unlocks"));
        Assert.Equal("community_identity_id", await ReadPrimaryKeyAsync(connection, "community_title_selections"));
        Assert.Equal("id", await ReadPrimaryKeyAsync(connection, "title_definitions"));
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
        Assert.DoesNotContain(foreignKeys, key => key.TableName == "title_definitions");
        Assert.Equal(
            [
                "ck_title_definitions_description_not_blank",
                "ck_title_definitions_description_trimmed",
                "ck_title_definitions_display_name_not_blank",
                "ck_title_definitions_display_name_trimmed"
            ],
            checkConstraints);
        Assert.Equal(2, histories.Length);
        Assert.Equal("Titles", histories[0].Owner);
        Assert.Equal(1L, histories[0].Version);
        Assert.Equal("CreateCommunityTitles", histories[0].Name);
        Assert.Equal(MigrationChecksum.Compute(versionOne.Sql), histories[0].Checksum);
        Assert.Equal("Titles", histories[1].Owner);
        Assert.Equal(2L, histories[1].Version);
        Assert.Equal("CreateTitleDefinitions", histories[1].Name);
        Assert.Equal(MigrationChecksum.Compute(versionTwo.Sql), histories[1].Checksum);
    }

    [Fact]
    public async Task AlreadyAppliedV1KeepsItsChecksumWhenV2IsApplied()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetTitlesMigrationAsync(factory);

        var migrations = new TitlesMigrationSource().GetMigrations().ToArray();
        var firstRun = await new MigrationRunner(
                factory,
                new FixedMigrationSource(migrations[0]))
            .RunAsync(TestToken);
        var secondRun = await new MigrationRunner(
                factory,
                new TitlesMigrationSource())
            .RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(1, 1), secondRun);
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

    [Fact]
    public async Task CreatePersistsCanonicalDefinitionWithoutCreatingCommunityState()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();

        var titleDefinitionId = await new CreateTitleDefinition(
                new TitleDefinitionStore(factory))
            .ExecuteAsync("  Veteran  ", "  Beschreibung  ", TestToken);

        var definition = await new GetTitleDefinition(
                new TitleDefinitionStore(factory))
            .ExecuteAsync(titleDefinitionId, TestToken);

        Assert.NotNull(definition);
        Assert.Equal(titleDefinitionId, definition!.Id);
        Assert.Equal("Veteran", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
        Assert.Equal(1, await ReadDefinitionCountAsync(factory, titleDefinitionId));
        Assert.Equal(0, await ReadRootCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadUnlockCountAsync(factory, communityIdentityId));
        Assert.Equal(0, await ReadSelectionCountAsync(factory, communityIdentityId));
    }

    [Fact]
    public async Task DuplicateDefinitionIdFailsWithoutCreatingADuplicate()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var titleDefinitionId = TitleDefinitionId.New();
        var definition = TitleDefinition.Create(titleDefinitionId, "Veteran", null);
        var store = new TitleDefinitionStore(factory);

        await store.AddAsync(definition, TestToken);

        await Assert.ThrowsAnyAsync<Exception>(() => store.AddAsync(definition, TestToken));

        Assert.Equal(1, await ReadDefinitionCountAsync(factory, titleDefinitionId));
    }

    [Fact]
    public async Task GetAndListReturnRehydratedDefinitionsInIdOrder()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var firstId = TitleDefinitionId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = TitleDefinitionId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var store = new TitleDefinitionStore(factory);

        Assert.Empty(await store.ListAsync(TestToken));
        await store.AddAsync(TitleDefinition.Create(secondId, "B", null), TestToken);
        await store.AddAsync(TitleDefinition.Create(firstId, "A", "Beschreibung"), TestToken);

        var list = await store.ListAsync(TestToken);
        var loaded = await store.GetAsync(firstId, TestToken);
        var unknown = await store.GetAsync(TitleDefinitionId.New(), TestToken);

        Assert.Equal([firstId, secondId], list.Select(definition => definition.Id).ToArray());
        Assert.Equal("A", loaded!.DisplayName);
        Assert.Equal("Beschreibung", loaded.Description);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task RenameAndDescriptionChangesPersistAndCanonicalNoOpsAvoidWrites()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var titleDefinitionId = TitleDefinitionId.New();
        var store = new TitleDefinitionStore(factory);
        await store.AddAsync(
            TitleDefinition.Create(titleDefinitionId, "A", "Old"),
            TestToken);

        Assert.True(await new RenameTitleDefinition(store)
            .ExecuteAsync(titleDefinitionId, "  B  ", TestToken));
        Assert.False(await new RenameTitleDefinition(store)
            .ExecuteAsync(titleDefinitionId, "B", TestToken));
        Assert.True(await new ChangeTitleDescription(store)
            .ExecuteAsync(titleDefinitionId, "  New  ", TestToken));
        Assert.False(await new ChangeTitleDescription(store)
            .ExecuteAsync(titleDefinitionId, "New", TestToken));
        Assert.True(await new ChangeTitleDescription(store)
            .ExecuteAsync(titleDefinitionId, "   ", TestToken));

        var definition = await store.GetAsync(titleDefinitionId, TestToken);

        Assert.Equal("B", definition!.DisplayName);
        Assert.Null(definition.Description);
    }

    [Fact]
    public async Task UnknownDefinitionMutationRollsBackAndLeavesCatalogUnchanged()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var existingId = TitleDefinitionId.New();
        var unknownId = TitleDefinitionId.New();
        var store = new TitleDefinitionStore(factory);
        await store.AddAsync(
            TitleDefinition.Create(existingId, "Existing", null),
            TestToken);

        var exception = await Assert.ThrowsAsync<TitleDefinitionNotFoundException>(() =>
            new RenameTitleDefinition(store).ExecuteAsync(
                unknownId,
                "Changed",
                TestToken));

        Assert.Equal(unknownId, exception.TitleDefinitionId);
        Assert.Equal(0, await ReadDefinitionCountAsync(factory, unknownId));
        Assert.Equal("Existing", (await store.GetAsync(existingId, TestToken))!.DisplayName);
    }

    [Fact]
    public async Task DatabaseConstraintsRejectNonCanonicalDefinitionValues()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareTitlesAsync(factory);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        const string sql = """
            INSERT INTO title_definitions (id, display_name, description)
            VALUES (@Id, @DisplayName, @Description);
            """;

        async Task AssertRejectedAsync(string displayName, string? description)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Id = Guid.NewGuid(),
                        DisplayName = displayName,
                        Description = description
                    },
                    cancellationToken: TestToken)));
        }

        await AssertRejectedAsync("   ", null);
        await AssertRejectedAsync(" Veteran ", null);
        await AssertRejectedAsync(new string('x', 101), null);
        await AssertRejectedAsync("Veteran", "   ");
        await AssertRejectedAsync("Veteran", " Old ");
        await AssertRejectedAsync("Veteran", new string('x', 501));

        await AssertRejectedAsync("\t", null);
        await AssertRejectedAsync("\tVeteran\t", null);
        await AssertRejectedAsync("\u00A0", null);
        await AssertRejectedAsync("\u00A0Veteran\u00A0", null);
        await AssertRejectedAsync("Veteran", "\t");
        await AssertRejectedAsync("Veteran", "\tBeschreibung\t");
        await AssertRejectedAsync("Veteran", "\u00A0");
        await AssertRejectedAsync("Veteran", "\u00A0Beschreibung\u00A0");
        await AssertRejectedAsync("Veteran", "\u2003");
    }

    [Fact]
    public async Task ConcurrentRenameAndDescriptionChangePreserveBothFields()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await using var renameFactory = CreateFactory();
        await using var descriptionFactory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var titleDefinitionId = TitleDefinitionId.New();
        await new TitleDefinitionStore(factory).AddAsync(
            TitleDefinition.Create(titleDefinitionId, "A", "Old"),
            TestToken);

        await Task.WhenAll(
            new RenameTitleDefinition(new TitleDefinitionStore(renameFactory))
                .ExecuteAsync(titleDefinitionId, "B", TestToken),
            new ChangeTitleDescription(new TitleDefinitionStore(descriptionFactory))
                .ExecuteAsync(titleDefinitionId, "New", TestToken));

        var definition = await new TitleDefinitionStore(factory)
            .GetAsync(titleDefinitionId, TestToken);

        Assert.Equal("B", definition!.DisplayName);
        Assert.Equal("New", definition.Description);
    }

    [Fact]
    public async Task ConcurrentRenamesOfOneDefinitionLeaveOneValidWinner()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await using var firstFactory = CreateFactory();
        await using var secondFactory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var titleDefinitionId = TitleDefinitionId.New();
        await new TitleDefinitionStore(factory).AddAsync(
            TitleDefinition.Create(titleDefinitionId, "A", null),
            TestToken);

        await Task.WhenAll(
            new RenameTitleDefinition(new TitleDefinitionStore(firstFactory))
                .ExecuteAsync(titleDefinitionId, "B", TestToken),
            new RenameTitleDefinition(new TitleDefinitionStore(secondFactory))
                .ExecuteAsync(titleDefinitionId, "C", TestToken));

        var definition = await new TitleDefinitionStore(factory)
            .GetAsync(titleDefinitionId, TestToken);

        Assert.Contains(definition!.DisplayName, new[] { "B", "C" });
        Assert.Equal(1, await ReadDefinitionCountAsync(factory, titleDefinitionId));
    }

    [Fact]
    public async Task ConcurrentChangesToDifferentDefinitionsPreserveBothChanges()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await using var firstFactory = CreateFactory();
        await using var secondFactory = CreateFactory();
        await PrepareTitlesAsync(factory);
        var firstId = TitleDefinitionId.New();
        var secondId = TitleDefinitionId.New();
        await new TitleDefinitionStore(factory).AddAsync(
            TitleDefinition.Create(firstId, "A", null),
            TestToken);
        await new TitleDefinitionStore(factory).AddAsync(
            TitleDefinition.Create(secondId, "B", null),
            TestToken);

        await Task.WhenAll(
            new RenameTitleDefinition(new TitleDefinitionStore(firstFactory))
                .ExecuteAsync(firstId, "A changed", TestToken),
            new RenameTitleDefinition(new TitleDefinitionStore(secondFactory))
                .ExecuteAsync(secondId, "B changed", TestToken));

        var store = new TitleDefinitionStore(factory);
        Assert.Equal("A changed", (await store.GetAsync(firstId, TestToken))!.DisplayName);
        Assert.Equal("B changed", (await store.GetAsync(secondId, TestToken))!.DisplayName);
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
                DROP TABLE IF EXISTS title_definitions;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Titles' AND version IN (1, 2);
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
                           is_nullable AS IsNullable,
                           character_maximum_length AS CharacterMaximumLength
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @TableName
                    ORDER BY ordinal_position;
                    """,
                    new { TableName = tableName },
                    cancellationToken: TestToken)))
            .ToArray();
    }

    private static async Task<string[]> ReadCheckConstraintNamesAsync(
        System.Data.Common.DbConnection connection,
        string tableName)
    {
        return (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT constraint_row.conname
                    FROM pg_constraint constraint_row
                    WHERE constraint_row.conrelid = to_regclass(@TableName)
                      AND constraint_row.contype = 'c'
                    ORDER BY constraint_row.conname;
                    """,
                    new { TableName = tableName },
                    cancellationToken: TestToken)))
            .ToArray();
    }

    private static async Task<int> ReadDefinitionCountAsync(
        PostgreSqlConnectionFactory factory,
        TitleDefinitionId titleDefinitionId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM title_definitions WHERE id = @Id;",
                new { Id = titleDefinitionId.Value },
                cancellationToken: TestToken));
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

        public int? CharacterMaximumLength { get; set; }
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

    private sealed class FixedMigrationSource : IMigrationSource
    {
        private readonly Migration migration;

        public FixedMigrationSource(Migration migration)
        {
            this.migration = migration;
        }

        public IEnumerable<Migration> GetMigrations()
        {
            yield return migration;
        }
    }
}
