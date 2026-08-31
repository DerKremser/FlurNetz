using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Achievements.Migrations;
using FlurNetz.Modules.Achievements.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Npgsql;

namespace FlurNetz.Modules.Achievements.IntegrationTests;

/// <summary>
/// Prüft Migration, Katalog, permanente Unlocks und PostgreSQL-Nebenläufigkeit.
/// </summary>
public sealed class AchievementsPostgreSqlIntegrationTests(AchievementsPostgreSqlFixture database)
    : IClassFixture<AchievementsPostgreSqlFixture>
{
    [Fact]
    public async Task MigrationCreatesBothOwnedTablesWithExpectedKeysAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetAchievementsMigrationAsync(factory);

        var migrationSource = new AchievementsMigrationSource();
        var runner = new MigrationRunner(factory, migrationSource);

        Assert.Equal(new MigrationRunResult(1, 0), await runner.RunAsync(TestToken));
        Assert.Equal(new MigrationRunResult(0, 1), await runner.RunAsync(TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tables = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name IN ('achievement_definitions', 'community_achievements')
                    ORDER BY table_name;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var definitionColumns = await ReadColumnsAsync(connection, "achievement_definitions");
        var achievementColumns = await ReadColumnsAsync(connection, "community_achievements");
        var foreignKeys = (await connection.QueryAsync<ForeignKeyInfo>(
                new CommandDefinition(
                    """
                    SELECT constraint_row.conname AS ConstraintName,
                           source_table.relname AS TableName,
                           target_table.relname AS ReferencedTable
                    FROM pg_constraint constraint_row
                    JOIN pg_class source_table ON source_table.oid = constraint_row.conrelid
                    JOIN pg_class target_table ON target_table.oid = constraint_row.confrelid
                    WHERE constraint_row.contype = 'f'
                      AND source_table.relnamespace = 'public'::regnamespace
                      AND source_table.relname IN ('achievement_definitions', 'community_achievements')
                    ORDER BY constraint_row.conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var migration = Assert.Single(migrationSource.GetMigrations());

        Assert.Equal(["achievement_definitions", "community_achievements"], tables);
        Assert.Equal(["id", "display_name", "description"], definitionColumns.Select(x => x.ColumnName));
        Assert.Equal(
            ["community_identity_id", "achievement_definition_id", "unlocked_at_utc"],
            achievementColumns.Select(x => x.ColumnName));
        Assert.Equal("id", await ReadPrimaryKeyAsync(connection, "achievement_definitions"));
        Assert.Equal(
            "community_identity_id,achievement_definition_id",
            await ReadPrimaryKeyAsync(connection, "community_achievements"));
        Assert.Single(foreignKeys);
        Assert.Equal("achievement_definitions", foreignKeys[0].ReferencedTable);
        Assert.DoesNotContain(foreignKeys, key => key.ReferencedTable == "community_identities");
        Assert.Equal("Achievements", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateAchievementDefinitionsAndCommunityAchievements", migration.Name);
        Assert.Equal(
            MigrationChecksum.Compute(migration.Sql),
            await ReadMigrationChecksumAsync(connection));
    }

    [Fact]
    public async Task DatabaseRejectsBlankAndNonCanonicalUnicodeDefinitionText()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        await using var connection = await factory.OpenConnectionAsync(TestToken);

        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO achievement_definitions (id, display_name, description) VALUES (@Id, @Name, NULL);",
                new { Id = Guid.NewGuid(), Name = " \u2003\u00a0 " },
                cancellationToken: TestToken)));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO achievement_definitions (id, display_name, description) VALUES (@Id, @Name, NULL);",
                new { Id = Guid.NewGuid(), Name = " Name " },
                cancellationToken: TestToken)));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO achievement_definitions (id, display_name, description) VALUES (@Id, @Name, @Description);",
                new { Id = Guid.NewGuid(), Name = "Name", Description = "\u202f" },
                cancellationToken: TestToken)));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO achievement_definitions (id, display_name, description) VALUES (@Id, @Name, @Description);",
                new { Id = Guid.NewGuid(), Name = "Name", Description = " Text" },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task DefinitionStoreSupportsCreateGetListDuplicateAndRehydration()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var store = new AchievementDefinitionStore(factory);
        var first = AchievementDefinition.Create(
            AchievementDefinitionId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            "Zweit",
            null);
        var second = AchievementDefinition.Create(
            AchievementDefinitionId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            "Erst",
            "Beschreibung");

        await store.AddAsync(first, TestToken);
        await store.AddAsync(second, TestToken);

        var loaded = await store.GetAsync(second.Id, TestToken);
        var list = await store.ListAsync(TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(second.DisplayName, loaded!.DisplayName);
        Assert.Equal(second.Description, loaded.Description);
        Assert.Equal([second.Id, first.Id], list.Select(x => x.Id));
        await Assert.ThrowsAsync<PostgresException>(() => store.AddAsync(
            AchievementDefinition.Rehydrate(first.Id, first.DisplayName, first.Description),
            TestToken));
    }

    [Fact]
    public async Task DefinitionMutationsAreAtomicAndNoOpWithoutUnnecessaryUpdate()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var store = new AchievementDefinitionStore(factory);
        var definition = AchievementDefinition.Create(AchievementDefinitionId.New(), "Alt", "Beschreibung");
        await store.AddAsync(definition, TestToken);

        Assert.False(await store.ExecuteAsync(
            definition.Id,
            value => value.Rename(" Alt "),
            TestToken));
        Assert.True(await store.ExecuteAsync(
            definition.Id,
            value => value.Rename("Neu"),
            TestToken));
        Assert.True(await store.ExecuteAsync(
            definition.Id,
            value => value.ChangeDescription(null),
            TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteAsync<bool>(
            definition.Id,
            _ => throw new InvalidOperationException("Testfehler"),
            TestToken));

        var loaded = await store.GetAsync(definition.Id, TestToken);
        Assert.Equal("Neu", loaded!.DisplayName);
        Assert.Null(loaded.Description);
        await Assert.ThrowsAsync<AchievementDefinitionNotFoundException>(() => store.ExecuteAsync(
            AchievementDefinitionId.New(),
            _ => true,
            TestToken));
    }

    [Fact]
    public async Task ConcurrentDefinitionMutationsDoNotLoseEitherChange()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var definition = AchievementDefinition.Create(AchievementDefinitionId.New(), "Basis", null);
        await new AchievementDefinitionStore(factory).AddAsync(definition, TestToken);
        var firstStore = new AchievementDefinitionStore(factory);
        var secondStore = new AchievementDefinitionStore(factory);

        var first = Task.Run(() => firstStore.ExecuteAsync(
            definition.Id,
            value =>
            {
                Thread.Sleep(100);
                value.Rename(value.DisplayName + "-A");
                return true;
            },
            TestToken),
            TestToken);
        var second = Task.Run(() => secondStore.ExecuteAsync(
            definition.Id,
            value =>
            {
                Thread.Sleep(100);
                value.Rename(value.DisplayName + "-B");
                return true;
            },
            TestToken),
            TestToken);

        await Task.WhenAll(first, second);

        var loaded = await firstStore.GetAsync(definition.Id, TestToken);
        Assert.Contains("-A", loaded!.DisplayName);
        Assert.Contains("-B", loaded.DisplayName);
    }

    [Fact]
    public async Task FirstUnlockWinsAndGetListUseThePersistedValues()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var firstDefinitionId = AchievementDefinitionId.New();
        var secondDefinitionId = AchievementDefinitionId.New();
        var definitionStore = new AchievementDefinitionStore(factory);
        await definitionStore.AddAsync(
            AchievementDefinition.Create(firstDefinitionId, "Erstes", null),
            TestToken);
        await definitionStore.AddAsync(
            AchievementDefinition.Create(secondDefinitionId, "Zweites", null),
            TestToken);

        var communityIdentityId = CommunityIdentityId.New();
        var firstTimestamp = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var laterTimestamp = firstTimestamp.AddHours(1);
        var store = new CommunityAchievementStore(factory);

        Assert.True(await store.UnlockAsync(
            CommunityAchievement.Create(communityIdentityId, firstDefinitionId, firstTimestamp),
            TestToken));
        Assert.False(await store.UnlockAsync(
            CommunityAchievement.Create(communityIdentityId, firstDefinitionId, laterTimestamp),
            TestToken));
        Assert.True(await store.UnlockAsync(
            CommunityAchievement.Create(communityIdentityId, secondDefinitionId, firstTimestamp),
            TestToken));

        var known = await store.GetAsync(communityIdentityId, firstDefinitionId, TestToken);
        var unknown = await store.GetAsync(communityIdentityId, AchievementDefinitionId.New(), TestToken);
        var list = await store.ListAsync(communityIdentityId, TestToken);

        Assert.NotNull(known);
        Assert.Equal(firstTimestamp, known!.UnlockedAtUtc);
        Assert.Null(unknown);
        Assert.Equal(
            new[] { firstDefinitionId, secondDefinitionId }.OrderBy(x => x.Value),
            list.Select(x => x.AchievementDefinitionId));
    }

    [Fact]
    public async Task UnknownDefinitionIsRejectedBeforeCommunityInsert()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var communityIdentityId = CommunityIdentityId.New();
        var definitionId = AchievementDefinitionId.New();
        var useCase = new UnlockCommunityAchievement(
            new AchievementDefinitionStore(factory),
            new CommunityAchievementStore(factory),
            new FixedClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<AchievementDefinitionNotFoundException>(() => useCase.ExecuteAsync(
            communityIdentityId,
            definitionId,
            TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_achievements WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task StructurallyValidCommunityWithoutIdentityRowIsAllowed()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var definitionId = AchievementDefinitionId.New();
        await new AchievementDefinitionStore(factory).AddAsync(
            AchievementDefinition.Create(definitionId, "Name", null),
            TestToken);
        var timestamp = new DateTimeOffset(2026, 8, 31, 13, 0, 0, TimeSpan.Zero);
        var result = await new UnlockCommunityAchievement(
            new AchievementDefinitionStore(factory),
            new CommunityAchievementStore(factory),
            new FixedClock(timestamp)).ExecuteAsync(
                CommunityIdentityId.New(),
                definitionId,
                TestToken);

        Assert.True(result);
    }

    [Fact]
    public async Task ConcurrentDuplicateUnlocksProduceExactlyOneRowAndOneWinner()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var definitionId = AchievementDefinitionId.New();
        await new AchievementDefinitionStore(factory).AddAsync(
            AchievementDefinition.Create(definitionId, "Name", null),
            TestToken);
        var communityIdentityId = CommunityIdentityId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var achievement = CommunityAchievement.Create(communityIdentityId, definitionId, timestamp);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            new CommunityAchievementStore(factory).UnlockAsync(achievement, TestToken)));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(7, results.Count(result => !result));
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM community_achievements WHERE community_identity_id = @CommunityIdentityId;",
                new { CommunityIdentityId = communityIdentityId.Value },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task ConcurrentDifferentAchievementsOfOneCommunityRemainIndependent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareAchievementsAsync(factory);
        var definitionStore = new AchievementDefinitionStore(factory);
        var definitions = Enumerable.Range(1, 4)
            .Select(_ => AchievementDefinitionId.New())
            .ToArray();
        foreach (var definitionId in definitions)
        {
            await definitionStore.AddAsync(
                AchievementDefinition.Create(definitionId, definitionId.Value.ToString(), null),
                TestToken);
        }

        var communityIdentityId = CommunityIdentityId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var results = await Task.WhenAll(definitions.Select(definitionId =>
            new CommunityAchievementStore(factory).UnlockAsync(
                CommunityAchievement.Create(communityIdentityId, definitionId, timestamp),
                TestToken)));

        Assert.All(results, Assert.True);
        var list = await new CommunityAchievementStore(factory).ListAsync(communityIdentityId, TestToken);
        Assert.Equal(definitions.OrderBy(x => x.Value), list.Select(x => x.AchievementDefinitionId).OrderBy(x => x.Value));
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static async Task PrepareAchievementsAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetAchievementsMigrationAsync(factory);
        await new MigrationRunner(factory, new AchievementsMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetAchievementsMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new AchievementsMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS community_achievements;
                DROP TABLE IF EXISTS achievement_definitions;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Achievements' AND version = 1;
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

    private static Task<string> ReadMigrationChecksumAsync(System.Data.Common.DbConnection connection)
    {
        return connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT checksum
                FROM flurnetz_persistence.migration_history
                WHERE owner = 'Achievements' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
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
    }
}
