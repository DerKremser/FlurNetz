using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Modules.Overlay.Migrations;
using FlurNetz.Modules.Overlay.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Overlay.IntegrationTests;

/// <summary>Prüft Overlay-Migration, Secrets, Locking, Cursor und Transaktionen gegen PostgreSQL.</summary>
public sealed class OverlayPostgreSqlIntegrationTests(OverlayPostgreSqlFixture database)
    : IClassFixture<OverlayPostgreSqlFixture>
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero).AddTicks(1_230);

    [Fact]
    public async Task MigrationIsIdempotentAndOwnsExactlyTheOverlayTables()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await ResetAsync(factory);
        var source = new OverlayMigrationSource();
        var first = await new MigrationRunner(factory, source).RunAsync(TestToken);
        var second = await new MigrationRunner(factory, source).RunAsync(TestToken);
        Assert.Equal(new MigrationRunResult(1, 0), first);
        Assert.Equal(new MigrationRunResult(0, 1), second);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tables = (await connection.QueryAsync<string>(new CommandDefinition("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'overlay_%' ORDER BY table_name;", cancellationToken: TestToken))).ToArray();
        Assert.Equal(["overlay_alerts", "overlay_channels"], tables);
        var history = await connection.QuerySingleAsync<MigrationHistory>(new CommandDefinition("SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum FROM flurnetz_persistence.migration_history WHERE owner = 'Overlay' AND version = 1;", cancellationToken: TestToken));
        Assert.Equal("Overlay", history.Owner);
        Assert.Equal(1, history.Version);
        Assert.Equal("CreateOverlayChannelsAndAlerts", history.Name);
        Assert.Equal(MigrationChecksum.Compute(source.GetMigrations().Single().Sql), history.Checksum);
    }

    [Fact]
    public async Task ChannelRoundtripRotationAndArchiveInvalidateSecretsWithoutExposingHash()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var channels = new PostgreSqlOverlayChannelStore(factory);
        var created = await new CreateOverlayChannel(channels, new FixedClock(Now)).ExecuteAsync("  OBS  ", "  Test  ", TestToken);
        Assert.Equal("OBS", created.Channel.DisplayName);
        Assert.Equal(43, created.SourceKey.Length);
        Assert.Null((await channels.GetAsync(created.Channel.Id, TestToken))!.GetType().GetProperty("SourceKeyHash"));
        Assert.NotNull(await channels.ResolveBySourceKeyAsync(created.SourceKey, TestToken));

        var rotated = await new RotateOverlaySourceKey(channels).ExecuteAsync(created.Channel.Id, TestToken);
        Assert.NotNull(rotated);
        Assert.NotEqual(created.SourceKey, rotated!.SourceKey);
        Assert.Null(await channels.ResolveBySourceKeyAsync(created.SourceKey, TestToken));
        Assert.NotNull(await channels.ResolveBySourceKeyAsync(rotated.SourceKey, TestToken));

        await new ArchiveOverlayChannel(channels, new FixedClock(Now.AddSeconds(1))).ExecuteAsync(created.Channel.Id, TestToken);
        Assert.Null(await channels.ResolveBySourceKeyAsync(rotated.SourceKey, TestToken));
        await Assert.ThrowsAsync<OverlayChannelArchivedException>(() => new RotateOverlaySourceKey(channels).ExecuteAsync(created.Channel.Id, TestToken));
    }

    [Fact]
    public async Task TransactionAwarePublishCommitsRollsBackAndPreviewBypassesDisabledState()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var channelStore = new PostgreSqlOverlayChannelStore(factory);
        var alertStore = new PostgreSqlOverlayAlertStore(factory);
        var created = await new CreateOverlayChannel(channelStore, new FixedClock(Now)).ExecuteAsync("Alerts", null, TestToken);
        var capability = new OverlayAlertPublishCapability(channelStore, alertStore, new FixedClock(Now));
        var request = new OverlayAlertPublishRequest(created.Channel.Id, "Committed", null, OverlayAlertVariant.Success, 5_000);

        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            Assert.Equal(OverlayAlertPublishStatus.ChannelDisabled, (await capability.PublishAsync(request, transaction.Connection, transaction.Transaction, TestToken)).Status);
            await transaction.CommitAsync(TestToken);
        }

        await new EnableOverlayChannel(channelStore, new FixedClock(Now.AddSeconds(1))).ExecuteAsync(created.Channel.Id, TestToken);
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            var result = await capability.PublishAsync(request, transaction.Connection, transaction.Transaction, TestToken);
            Assert.Equal(OverlayAlertPublishStatus.Published, result.Status);
            await transaction.CommitAsync(TestToken);
        }
        await using (var transaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            var result = await capability.PublishAsync(request with { Title = "Rolled back" }, transaction.Connection, transaction.Transaction, TestToken);
            Assert.True(result.IsPublished);
            await transaction.RollbackAsync(TestToken);
        }

        var preview = await new PublishPreviewAlert(factory, channelStore, alertStore, new FixedClock(Now.AddSeconds(2))).ExecuteAsync(request with { Title = "Preview" }, TestToken);
        Assert.Equal(OverlayAlertPublishStatus.Published, preview.Status);
        var alerts = await alertStore.ReadAfterAsync(created.Channel.Id, OverlayAlertCursor.Start(created.Channel.Id), Now.AddSeconds(2), 100, TestToken);
        Assert.Equal(["Committed", "Preview"], alerts.Select(alert => alert.Title).ToArray());
    }

    [Fact]
    public async Task CursorUsesCreatedAtAndIdOrderAndExcludesExpiredAlerts()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var channelStore = new PostgreSqlOverlayChannelStore(factory);
        var alertStore = new PostgreSqlOverlayAlertStore(factory);
        var created = await new CreateOverlayChannel(channelStore, new FixedClock(Now)).ExecuteAsync("Alerts", null, TestToken);
        var firstId = OverlayAlertId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = OverlayAlertId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        await alertStore.AddAsync(OverlayAlert.Create(firstId, created.Channel.Id, "First", null, OverlayAlertVariant.Default, 5_000, null, Now), TestToken);
        await alertStore.AddAsync(OverlayAlert.Create(secondId, created.Channel.Id, "Second", null, OverlayAlertVariant.Default, 5_000, null, Now), TestToken);
        await alertStore.AddAsync(OverlayAlert.Create(OverlayAlertId.New(), created.Channel.Id, "Expired", null, OverlayAlertVariant.Default, 1_000, null, Now.AddMinutes(-1)), TestToken);

        var firstPage = await alertStore.ReadAfterAsync(created.Channel.Id, OverlayAlertCursor.Start(created.Channel.Id), Now.AddSeconds(1), 1, TestToken);
        var cursor = OverlayAlertCursor.Create(created.Channel.Id, firstPage.Single().CreatedAtUtc, firstPage.Single().Id.Value);
        var secondPage = await alertStore.ReadAfterAsync(created.Channel.Id, cursor, Now.AddSeconds(1), 10, TestToken);
        Assert.Equal(["Second"], secondPage.Select(alert => alert.Title).ToArray());
    }

    private async Task PrepareAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetAsync(factory);
        await new MigrationRunner(factory, new OverlayMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetAsync(PostgreSqlConnectionFactory factory)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition("DROP TABLE IF EXISTS overlay_alerts; DROP TABLE IF EXISTS overlay_channels; DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;", cancellationToken: TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));
    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private sealed class FixedClock(DateTimeOffset utcNow) : IClock { public DateTimeOffset UtcNow { get; } = utcNow; }
    private sealed class MigrationHistory { public string Owner { get; set; } = string.Empty; public int Version { get; set; } public string Name { get; set; } = string.Empty; public string Checksum { get; set; } = string.Empty; }
}
