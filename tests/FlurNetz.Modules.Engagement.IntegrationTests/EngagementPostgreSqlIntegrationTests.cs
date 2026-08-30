using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Engagement.Migrations;
using FlurNetz.Modules.Engagement.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Engagement.IntegrationTests;

/// <summary>
/// Prüft Migration, Recording-Use-Case und Persistence-Adapter des Engagement-Slices.
/// </summary>
public sealed class EngagementPostgreSqlIntegrationTests(EngagementPostgreSqlFixture database)
    : IClassFixture<EngagementPostgreSqlFixture>
{
    private static readonly DateTimeOffset TestNow =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EngagementMigrationCreatesExactTableWithoutIdentityForeignKeyAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetEngagementMigrationAsync(factory);

        var migrationSource = new EngagementMigrationSource();
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
                WHERE table_schema = 'public' AND table_name = 'engagement_activities'
                ORDER BY ordinal_position;
                """,
                cancellationToken: TestToken))).ToArray();
        var primaryKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'engagement_activities'::regclass AND contype = 'p';
                """,
                cancellationToken: TestToken));
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'engagement_activities'::regclass AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Engagement' AND version = 1;
                """,
                cancellationToken: TestToken));

        var migration = Assert.Single(migrationSource.GetMigrations());
        Assert.Equal("Engagement", history.Owner);
        Assert.Equal(1L, history.Version);
        Assert.Equal("CreateEngagementActivities", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        Assert.Equal(
            ["id", "community_identity_id", "activity_type", "occurred_at_utc"],
            columns.Select(column => column.ColumnName).ToArray());
        Assert.Equal(["uuid", "uuid", "text", "timestamp with time zone"], columns.Select(column => column.DataType).ToArray());
        Assert.All(columns, column => Assert.Equal("NO", column.IsNullable));
        Assert.Equal(1, primaryKeyCount);
        Assert.Equal(0, foreignKeyCount);
    }

    [Fact]
    public async Task RecordUseCasePersistsOneMessageActivityWithoutIdentityTable()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);

        var repository = new EngagementActivityRepository(factory);
        var useCase = new RecordMessageEngagement(
            new RepositoryMessageEngagementRecorder(repository),
            new FixedClock(TestNow));
        var communityIdentityId = CommunityIdentityId.New();

        var id = await useCase.ExecuteAsync(communityIdentityId, TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var row = await connection.QuerySingleAsync<ActivityRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, community_identity_id AS CommunityIdentityId,
                       activity_type AS ActivityType, occurred_at_utc AS OccurredAtUtc
                FROM engagement_activities;
                """,
                cancellationToken: TestToken));

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(id.Value, row.Id);
        Assert.Equal(communityIdentityId.Value, row.CommunityIdentityId);
        Assert.Equal("message", row.ActivityType);
        Assert.Equal(TestNow, row.OccurredAtUtc);
    }

    [Fact]
    public async Task RepositoryLoadsAllMessageValuesAndPreservesUtcTimestamp()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);
        var id = EngagementActivityId.New();
        var communityIdentityId = CommunityIdentityId.New();
        var activity = EngagementActivity.CreateMessage(id, communityIdentityId, TestNow);

        await repository.AddAsync(activity, TestToken);
        var loaded = await repository.GetByIdAsync(id, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded!.Id);
        Assert.Equal(communityIdentityId, loaded.CommunityIdentityId);
        Assert.Equal(EngagementActivityType.Message, loaded.Type);
        Assert.Equal(TestNow, loaded.OccurredAtUtc);
        Assert.Equal(TimeSpan.Zero, loaded.OccurredAtUtc.Offset);
    }

    [Fact]
    public async Task RepositoryReturnsNullForAnUnknownValidActivityId()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);

        var loaded = await repository.GetByIdAsync(EngagementActivityId.New(), TestToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task RepositoryRejectsDuplicatePrimaryKeyInsteadOfUpserting()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);
        var activity = EngagementActivity.CreateMessage(
            EngagementActivityId.New(),
            CommunityIdentityId.New(),
            TestNow);

        await repository.AddAsync(activity, TestToken);
        await Assert.ThrowsAnyAsync<Exception>(() => repository.AddAsync(activity, TestToken));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM engagement_activities;", cancellationToken: TestToken));

        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task RepositoryWriteIsRolledBackAfterAnInsertError()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);
        var activity = EngagementActivity.CreateMessage(
            EngagementActivityId.New(),
            CommunityIdentityId.New(),
            TestNow);

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await repository.AddAsync(activity, transaction, TestToken);
            await Assert.ThrowsAnyAsync<Exception>(() => repository.AddAsync(activity, transaction, TestToken));
            await transaction.RollbackAsync(TestToken);
        }

        Assert.Null(await repository.GetByIdAsync(activity.Id, TestToken));
    }

    [Fact]
    public async Task RecordUseCaseStoresMultipleMessagesForTheSameIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);
        var useCase = new RecordMessageEngagement(
            new RepositoryMessageEngagementRecorder(repository),
            new FixedClock(TestNow));
        var communityIdentityId = CommunityIdentityId.New();

        var ids = new[]
        {
            await useCase.ExecuteAsync(communityIdentityId, TestToken),
            await useCase.ExecuteAsync(communityIdentityId, TestToken),
            await useCase.ExecuteAsync(communityIdentityId, TestToken)
        };

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var rowCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM engagement_activities;", cancellationToken: TestToken));
        var identityCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM engagement_activities WHERE community_identity_id = @Id;",
                new { Id = communityIdentityId.Value },
                cancellationToken: TestToken));

        Assert.Equal(3, ids.Distinct().Count());
        Assert.Equal(3, rowCount);
        Assert.Equal(3, identityCount);
    }

    [Fact]
    public async Task RepositoryRejectsUnknownPersistedActivityType()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareEngagementAsync(factory);
        var repository = new EngagementActivityRepository(factory);
        var id = EngagementActivityId.New();
        var communityIdentityId = CommunityIdentityId.New();

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO engagement_activities
                    (id, community_identity_id, activity_type, occurred_at_utc)
                VALUES (@Id, @CommunityIdentityId, @ActivityType, @OccurredAtUtc);
                """,
                new
                {
                    Id = id.Value,
                    CommunityIdentityId = communityIdentityId.Value,
                    ActivityType = "future-activity",
                    OccurredAtUtc = TestNow
                },
                cancellationToken: TestToken));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.GetByIdAsync(id, TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareEngagementAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetEngagementMigrationAsync(factory);
        await new MigrationRunner(factory, new EngagementMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetEngagementMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new EngagementMigrationSource()).RunAsync(TestToken);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS engagement_activities;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Engagement' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class RepositoryMessageEngagementRecorder(IEngagementActivityRepository repository)
        : IMessageEngagementRecorder
    {
        public Task RecordAsync(
            EngagementActivity activity,
            IntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return repository.AddAsync(activity, cancellationToken);
        }
    }

    private sealed class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class ActivityRow
    {
        public Guid Id { get; set; }

        public Guid CommunityIdentityId { get; set; }

        public string ActivityType { get; set; } = string.Empty;

        public DateTimeOffset OccurredAtUtc { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
