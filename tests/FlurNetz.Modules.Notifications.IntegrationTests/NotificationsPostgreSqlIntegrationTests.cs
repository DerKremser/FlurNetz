using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Modules.Notifications.Migrations;
using FlurNetz.Modules.Notifications.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Notifications.IntegrationTests;

/// <summary>
/// Prüft Migration, Dapper-Store, Pagination und Lebenszyklus gegen echtes PostgreSQL.
/// </summary>
public sealed class NotificationsPostgreSqlIntegrationTests(NotificationsPostgreSqlFixture database)
    : IClassFixture<NotificationsPostgreSqlFixture>
{
    private static readonly DateTimeOffset FirstTime =
        new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task MigrationIsIdempotentAndCreatesTheOwnedSchemaWithoutForeignKeys()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory);

        var first = await new MigrationRunner(factory, new NotificationsMigrationSource()).RunAsync(TestToken);
        var second = await new MigrationRunner(factory, new NotificationsMigrationSource()).RunAsync(TestToken);

        Assert.Equal(1, first.AppliedCount);
        Assert.Equal(0, second.AppliedCount);
        Assert.Equal(1, second.SkippedCount);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(1L, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'community_notifications';",
                cancellationToken: TestToken)));
        Assert.Equal(0L, await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM information_schema.table_constraints WHERE table_name = 'community_notifications' AND constraint_type = 'FOREIGN KEY';",
                cancellationToken: TestToken)));
        Assert.Equal("CreateCommunityNotifications", await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                "SELECT name FROM flurnetz_persistence.migration_history WHERE owner = 'Notifications' AND version = 1;",
                cancellationToken: TestToken)));
        Assert.Equal(64, (await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                "SELECT checksum FROM flurnetz_persistence.migration_history WHERE owner = 'Notifications' AND version = 1;",
                cancellationToken: TestToken))).Length);
    }

    [Fact]
    public async Task StoreSupportsSnapshotRoundtripIdentityIsolationPaginationAndUnreadLifecycle()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var store = new CommunityNotificationStore(factory);
        var identity = CommunityIdentityId.New();
        var otherIdentity = CommunityIdentityId.New();
        var notifications = Enumerable.Range(0, 5)
            .Select(index => CommunityNotification.Create(
                NotificationId.New(),
                identity,
                "shop.purchase-completed",
                $"Kauf {index}",
                index == 0 ? "Snapshot" : null,
                index == 0 ? new NotificationSourceReference("shop.purchase", $"purchase-{index}") : null,
                FirstTime.AddMinutes(index)))
            .ToArray();
        var other = CommunityNotification.Create(
            NotificationId.New(),
            otherIdentity,
            "system.notice",
            "Andere Identity",
            null,
            null,
            FirstTime.AddHours(1));

        foreach (var notification in notifications.Append(other))
        {
            await store.AddAsync(notification, TestToken);
        }

        var loaded = await store.GetAsync(notifications[0].Id, TestToken);
        Assert.NotNull(loaded);
        Assert.Equal("Snapshot", loaded!.Message);
        Assert.Equal(notifications[0].SourceReference, loaded.SourceReference);
        Assert.Null(await store.GetForIdentityAsync(otherIdentity, notifications[0].Id, TestToken));

        var firstPage = await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, pageSize: 2, cancellationToken: TestToken);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(notifications[4].Id, firstPage.Items[0].Id);
        Assert.NotNull(firstPage.NextCursor);

        var newerNotification = CommunityNotification.Create(
            NotificationId.New(),
            identity,
            "system.notice",
            "Neuere Notification",
            null,
            null,
            FirstTime.AddHours(2));
        await store.AddAsync(newerNotification, TestToken);

        var secondPage = await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, firstPage.NextCursor, pageSize: 2, cancellationToken: TestToken);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Equal(notifications[2].Id, secondPage.Items[0].Id);
        Assert.DoesNotContain(newerNotification, secondPage.Items);
        Assert.DoesNotContain(firstPage.Items, item => secondPage.Items.Contains(item));
        Assert.NotNull(secondPage.NextCursor);

        var thirdPage = await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, secondPage.NextCursor, pageSize: 2, cancellationToken: TestToken);
        Assert.Single(thirdPage.Items);
        Assert.Null(thirdPage.NextCursor);
        Assert.Equal(5, firstPage.Items.Concat(secondPage.Items).Concat(thirdPage.Items).Distinct().Count());

        Assert.Equal(6L, await store.CountUnreadForIdentityAsync(identity, TestToken));
        Assert.True(await store.MarkReadAsync(identity, notifications[0].Id, FirstTime.AddDays(1), TestToken));
        Assert.True(await store.MarkReadAsync(identity, notifications[0].Id, FirstTime.AddDays(2), TestToken));
        Assert.Equal(FirstTime.AddDays(1), (await store.GetAsync(notifications[0].Id, TestToken))!.ReadAtUtc);
        Assert.Equal(5L, await store.CountUnreadForIdentityAsync(identity, TestToken));
        Assert.True(await store.MarkUnreadAsync(identity, notifications[0].Id, TestToken));
        Assert.True(await store.MarkUnreadAsync(identity, notifications[0].Id, TestToken));
        Assert.Equal(6L, await store.CountUnreadForIdentityAsync(identity, TestToken));

        Assert.Equal(6L, await store.MarkAllReadAsync(identity, FirstTime.AddDays(3), TestToken));
        Assert.Equal(0L, await store.MarkAllReadAsync(identity, FirstTime.AddDays(4), TestToken));
        Assert.Empty((await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, unreadOnly: true, cancellationToken: TestToken)).Items);
        Assert.Equal(1L, await store.CountUnreadForIdentityAsync(otherIdentity, TestToken));
    }

    [Fact]
    public async Task TransactionAwareInsertCommitsWithCallerAndRollsBackWithCaller()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var store = new CommunityNotificationStore(factory);
        var identity = CommunityIdentityId.New();
        var committed = CreateNotification(identity, "commit");
        var rolledBack = CreateNotification(identity, "rollback");

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await store.AddAsync(committed, transaction.Connection, transaction.Transaction, TestToken);
            await transaction.CommitAsync(TestToken);
        }

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            await store.AddAsync(rolledBack, transaction.Connection, transaction.Transaction, TestToken);
            await transaction.RollbackAsync(TestToken);
        }

        Assert.NotNull(await store.GetAsync(committed.Id, TestToken));
        Assert.Null(await store.GetAsync(rolledBack.Id, TestToken));
    }

    [Fact]
    public async Task DatabaseRejectsInconsistentSourceReference()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        await using var connection = await factory.OpenConnectionAsync(TestToken);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO community_notifications
                    (id, community_identity_id, notification_type, title, source_type, created_at_utc)
                VALUES (@Id, @IdentityId, 'system.notice', 'Title', 'system', @CreatedAtUtc);
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    IdentityId = Guid.NewGuid(),
                    CreatedAtUtc = FirstTime
                },
                cancellationToken: TestToken)));
    }

    [Fact]
    public async Task SameTimestampUsesDeterministicNotificationIdDescendingOrder()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var store = new CommunityNotificationStore(factory);
        var identity = CommunityIdentityId.New();
        var lowerId = NotificationId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherId = NotificationId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var lower = CommunityNotification.Create(
            lowerId, identity, "system.notice", "Lower", null, null, FirstTime);
        var higher = CommunityNotification.Create(
            higherId, identity, "system.notice", "Higher", null, null, FirstTime);
        await store.AddAsync(lower, TestToken);
        await store.AddAsync(higher, TestToken);

        var page = await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, pageSize: 1, cancellationToken: TestToken);

        Assert.Equal(higherId, Assert.Single(page.Items).Id);
        Assert.NotNull(page.NextCursor);
        var nextPage = await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, page.NextCursor, pageSize: 1, cancellationToken: TestToken);
        Assert.Equal(lowerId, Assert.Single(nextPage.Items).Id);
        Assert.Null(nextPage.NextCursor);
    }

    [Fact]
    public async Task ParallelReadMutationsRemainIdentityScopedAndConsistent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareDatabaseAsync(factory);
        var store = new CommunityNotificationStore(factory);
        var identity = CommunityIdentityId.New();
        var otherIdentity = CommunityIdentityId.New();
        var notifications = Enumerable.Range(0, 20)
            .Select(index => CommunityNotification.Create(
                NotificationId.New(), identity, "system.notice", $"Parallel {index}", null, null,
                FirstTime.AddMinutes(index)))
            .ToArray();
        await Task.WhenAll(notifications.Select(notification => store.AddAsync(notification, TestToken)));

        await Task.WhenAll(notifications.Select(notification => store.MarkReadAsync(
            identity,
            notification.Id,
            FirstTime.AddDays(1),
            TestToken)));

        Assert.Equal(0L, await store.CountUnreadForIdentityAsync(identity, TestToken));
        Assert.Equal(0L, await store.CountUnreadForIdentityAsync(otherIdentity, TestToken));
        Assert.Equal(20, (await new ListNotificationsForIdentity(store)
            .ExecuteAsync(identity, cancellationToken: TestToken)).Items.Count);
    }

    private async Task PrepareDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetDatabaseAsync(factory);
        await new MigrationRunner(factory, new NotificationsMigrationSource()).RunAsync(TestToken);
    }

    private async Task ResetDatabaseAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DROP TABLE IF EXISTS community_notifications; DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;",
            cancellationToken: TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static CommunityNotification CreateNotification(
        CommunityIdentityId identity,
        string suffix) => CommunityNotification.Create(
            NotificationId.New(),
            identity,
            "system.notice",
            suffix,
            null,
            null,
            FirstTime);

    private void SkipIfDatabaseIsUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
