using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Inventory.IntegrationTests;

/// <summary>
/// Prüft Migration, Use Cases, Sparse-Lifecycle, Persistence-Adapter und PostgreSQL-Konkurrenzschutz.
/// </summary>
public sealed class InventoryPostgreSqlIntegrationTests(InventoryPostgreSqlFixture database)
    : IClassFixture<InventoryPostgreSqlFixture>
{
    [Fact]
    public async Task InventoryMigrationCreatesExactCompositeTableWithoutCrossModuleForeignKeysAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetInventoryMigrationAsync(factory);

        var migrationSource = new InventoryMigrationSource();
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
                WHERE table_schema = 'public' AND table_name = 'community_inventory_entries'
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
                WHERE constraint_row.conrelid = 'community_inventory_entries'::regclass
                  AND constraint_row.contype = 'p';
                """,
                cancellationToken: TestToken));
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'community_inventory_entries'::regclass AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var checkConstraint = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                """
                SELECT pg_get_constraintdef(oid)
                FROM pg_constraint
                WHERE conrelid = 'community_inventory_entries'::regclass
                  AND contype = 'c'
                  AND pg_get_constraintdef(oid) LIKE '%quantity%>=%0%';
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Inventory' AND version = 1;
                """,
                cancellationToken: TestToken));

        var migration = Assert.Single(migrationSource.GetMigrations());
        Assert.Equal("Inventory", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateCommunityInventoryEntries", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        Assert.Equal(
            ["community_identity_id", "item_definition_id", "quantity"],
            columns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(
            ["uuid", "uuid", "bigint"],
            columns.Select(column => column.DataType).ToArray());
        Assert.All(columns, column => Assert.Equal("NO", column.IsNullable));
        Assert.Equal("community_identity_id,item_definition_id", primaryKey);
        Assert.Equal(0, foreignKeyCount);
        Assert.Contains("quantity", checkConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">= 0", checkConstraint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstAddLazilyCreatesInventoryEntryWithoutIdentityOrItemCatalogTables()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var useCase = CreateAddUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var result = await useCase.ExecuteAsync(
            communityIdentityId,
            itemDefinitionId,
            5,
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<InventoryRow>(
            new CommandDefinition(
                """
                SELECT community_identity_id AS CommunityIdentityId,
                       item_definition_id AS ItemDefinitionId,
                       quantity AS Quantity
                FROM community_inventory_entries;
                """,
                cancellationToken: TestToken));

        Assert.Equal(5, result.Value);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal(itemDefinitionId.Value, row.ItemDefinitionId);
        Assert.Equal(5, row.Quantity);
    }

    [Fact]
    public async Task SubsequentAddsAndLoadReturnTheAccumulatedDomainState()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var store = new CommunityInventoryStore(factory);
        var useCase = new AddInventoryQuantity(store);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var firstResult = await useCase.ExecuteAsync(communityIdentityId, itemDefinitionId, 5, TestToken);
        var secondResult = await useCase.ExecuteAsync(communityIdentityId, itemDefinitionId, 7, TestToken);
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(5, firstResult.Value);
        Assert.Equal(12, secondResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(communityIdentityId, loaded!.CommunityIdentityId);
        Assert.Equal(itemDefinitionId, loaded.ItemDefinitionId);
        Assert.Equal(12, loaded.Quantity.Value);
    }

    [Fact]
    public async Task StoreReturnsNullForUnknownValidEntryWithoutWritingAZeroRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var store = new CommunityInventoryStore(factory);

        var loaded = await store.GetAsync(
            CommunityIdentityId.New(),
            ItemDefinitionId.New(),
            TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_inventory_entries;",
                cancellationToken: TestToken));

        Assert.Null(loaded);
        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task InvalidFirstAddLeavesNoLazyInventoryRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var useCase = CreateAddUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, itemDefinitionId, 0, TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(communityIdentityId, itemDefinitionId, -1, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_inventory_entries;",
                cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task ExistingRemovePersistsTheReducedQuantity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var remove = CreateRemoveUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 10, TestToken);
        var result = await remove.ExecuteAsync(communityIdentityId, itemDefinitionId, 3, TestToken);
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(7, result.Value);
        Assert.NotNull(loaded);
        Assert.Equal(7, loaded!.Quantity.Value);
    }

    [Fact]
    public async Task ExactRemoveDeletesTheZeroQuantityRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var remove = CreateRemoveUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 10, TestToken);
        var result = await remove.ExecuteAsync(communityIdentityId, itemDefinitionId, 10, TestToken);
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_inventory_entries;",
                cancellationToken: TestToken));

        Assert.Equal(InventoryQuantity.Zero, result);
        Assert.Null(loaded);
        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task RemoveOnMissingEntryRejectsWithoutCreatingARow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var remove = CreateRemoveUseCase(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await Assert.ThrowsAsync<InsufficientInventoryQuantityException>(
            () => remove.ExecuteAsync(communityIdentityId, itemDefinitionId, 1, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_inventory_entries;",
                cancellationToken: TestToken));

        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task InsufficientRemoveRollsBackAndPreservesExistingQuantity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var remove = CreateRemoveUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 5, TestToken);
        await Assert.ThrowsAsync<InsufficientInventoryQuantityException>(
            () => remove.ExecuteAsync(communityIdentityId, itemDefinitionId, 6, TestToken));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded!.Quantity.Value);
    }

    [Fact]
    public async Task AddOverflowRollsBackAndPreservesMaximumQuantity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var firstResult = await add.ExecuteAsync(
            communityIdentityId,
            itemDefinitionId,
            long.MaxValue,
            TestToken);
        await Assert.ThrowsAsync<OverflowException>(
            () => add.ExecuteAsync(communityIdentityId, itemDefinitionId, 1, TestToken));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(long.MaxValue, firstResult.Value);
        Assert.NotNull(loaded);
        Assert.Equal(long.MaxValue, loaded!.Quantity.Value);
    }

    [Fact]
    public async Task DatabaseCheckRejectsNegativeQuantity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO community_inventory_entries
                    (community_identity_id, item_definition_id, quantity)
                VALUES
                    (@CommunityIdentityId, @ItemDefinitionId, @Quantity);
                """,
                new
                {
                    CommunityIdentityId = CommunityIdentityId.New().Value,
                    ItemDefinitionId = ItemDefinitionId.New().Value,
                    Quantity = -1L
                },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DifferentItemDefinitionsRemainIndependentForSameIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var firstItemDefinitionId = ItemDefinitionId.New();
        var secondItemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, firstItemDefinitionId, 3, TestToken);
        await add.ExecuteAsync(communityIdentityId, secondItemDefinitionId, 7, TestToken);

        var first = await store.GetAsync(communityIdentityId, firstItemDefinitionId, TestToken);
        var second = await store.GetAsync(communityIdentityId, secondItemDefinitionId, TestToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(3, first!.Quantity.Value);
        Assert.Equal(7, second!.Quantity.Value);
    }

    [Fact]
    public async Task SameItemDefinitionRemainsIndependentAcrossIdentities()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var firstIdentityId = CommunityIdentityId.New();
        var secondIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(firstIdentityId, itemDefinitionId, 4, TestToken);
        await add.ExecuteAsync(secondIdentityId, itemDefinitionId, 9, TestToken);

        var first = await store.GetAsync(firstIdentityId, itemDefinitionId, TestToken);
        var second = await store.GetAsync(secondIdentityId, itemDefinitionId, TestToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(4, first!.Quantity.Value);
        Assert.Equal(9, second!.Quantity.Value);
    }

    [Fact]
    public async Task TwentyConcurrentFirstAddsProduceExactlyTwentyUnits()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => add.ExecuteAsync(communityIdentityId, itemDefinitionId, 1, TestToken)));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(20, results.Length);
        Assert.NotNull(loaded);
        Assert.Equal(20, loaded!.Quantity.Value);
    }

    [Fact]
    public async Task TwentyConcurrentAddsOnExistingEntryProduceExactAccumulatedQuantity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 10, TestToken);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => add.ExecuteAsync(communityIdentityId, itemDefinitionId, 1, TestToken)));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(20, results.Length);
        Assert.NotNull(loaded);
        Assert.Equal(30, loaded!.Quantity.Value);
    }

    [Fact]
    public async Task TwentyConcurrentRemovesFromTwentyUnitsAllSucceedAndDeleteEntry()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var remove = CreateRemoveUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 20, TestToken);
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => ExecuteRemoveAndClassifyAsync(
                    remove,
                    communityIdentityId,
                    itemDefinitionId)));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.All(outcomes, outcome => Assert.Equal(RemoveOutcome.Success, outcome));
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TwentyConcurrentRemovesAgainstTenUnitsHaveExactlyTenSuccessfulRemoves()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareInventoryAsync(factory);
        var add = CreateAddUseCase(factory);
        var remove = CreateRemoveUseCase(factory);
        var store = new CommunityInventoryStore(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        await add.ExecuteAsync(communityIdentityId, itemDefinitionId, 10, TestToken);
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => ExecuteRemoveAndClassifyAsync(
                    remove,
                    communityIdentityId,
                    itemDefinitionId)));
        var loaded = await store.GetAsync(communityIdentityId, itemDefinitionId, TestToken);

        Assert.Equal(10, outcomes.Count(outcome => outcome == RemoveOutcome.Success));
        Assert.Equal(10, outcomes.Count(outcome => outcome == RemoveOutcome.Insufficient));
        Assert.Null(loaded);
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private AddInventoryQuantity CreateAddUseCase(PostgreSqlConnectionFactory factory) =>
        new(new CommunityInventoryStore(factory));

    private RemoveInventoryQuantity CreateRemoveUseCase(PostgreSqlConnectionFactory factory) =>
        new(new CommunityInventoryStore(factory));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareInventoryAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetInventoryMigrationAsync(factory);
        await new MigrationRunner(factory, new InventoryMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetInventoryMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new InventoryMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_inventory_entries;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Inventory' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private static async Task<RemoveOutcome> ExecuteRemoveAndClassifyAsync(
        RemoveInventoryQuantity useCase,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId)
    {
        try
        {
            await useCase.ExecuteAsync(
                communityIdentityId,
                itemDefinitionId,
                1,
                TestToken);
            return RemoveOutcome.Success;
        }
        catch (InsufficientInventoryQuantityException)
        {
            return RemoveOutcome.Insufficient;
        }
    }

    private enum RemoveOutcome
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

    private sealed class InventoryRow
    {
        public Guid CommunityIdentityId { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public long Quantity { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
