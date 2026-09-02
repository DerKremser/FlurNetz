using Dapper;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using System.Data.Common;

namespace FlurNetz.Modules.Overlay.Persistence;

/// <summary>Persistiert Alerts, Cursor und das begrenzte Replay-Fenster.</summary>
public sealed class PostgreSqlOverlayAlertStore : IOverlayAlertStore
{
    private const string InsertSql = """
        INSERT INTO overlay_alerts
            (id, overlay_channel_id, title, message, variant, duration_milliseconds,
             source_type, source_id, created_at_utc, expires_at_utc)
        VALUES
            (@Id, @ChannelId, @Title, @Message, @Variant, @DurationMilliseconds,
             @SourceType, @SourceId, @CreatedAtUtc, @ExpiresAtUtc);
        """;
    private const string TailSql = """
        SELECT created_at_utc AS CreatedAtUtc, id AS AlertId
        FROM overlay_alerts
        WHERE overlay_channel_id = @ChannelId
        ORDER BY created_at_utc DESC, id DESC
        LIMIT 1;
        """;
    private const string ReadAfterSql = """
        SELECT id AS Id, overlay_channel_id AS ChannelId, title AS Title, message AS Message,
               variant AS Variant, duration_milliseconds AS DurationMilliseconds,
               source_type AS SourceType, source_id AS SourceId,
               created_at_utc AS CreatedAtUtc, expires_at_utc AS ExpiresAtUtc
        FROM overlay_alerts
        WHERE overlay_channel_id = @ChannelId
          AND expires_at_utc > @NowUtc
          AND created_at_utc >= @ReplaySinceUtc
          AND (created_at_utc > @CursorCreatedAtUtc
               OR (created_at_utc = @CursorCreatedAtUtc AND id > @CursorAlertId))
        ORDER BY created_at_utc ASC, id ASC
        LIMIT @Take;
        """;
    private const string CleanupSql = """
        DELETE FROM overlay_alerts
        WHERE id IN
        (
            SELECT id
            FROM overlay_alerts
            WHERE overlay_channel_id = @ChannelId
              AND expires_at_utc <= @NowUtc
            ORDER BY expires_at_utc ASC, id ASC
            LIMIT @CleanupBatchSize
        );
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>Erstellt den Alert-Store.</summary>
    public PostgreSqlOverlayAlertStore(IPostgreSqlConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(OverlayAlert alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            await AddAsync(alert, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(OverlayAlert alert, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            Id = alert.Id.Value,
            ChannelId = alert.ChannelId.Value,
            alert.Title,
            alert.Message,
            alert.Variant,
            alert.DurationMilliseconds,
            SourceType = alert.SourceReference?.SourceType,
            SourceId = alert.SourceReference?.SourceId,
            alert.CreatedAtUtc,
            alert.ExpiresAtUtc
        }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (inserted != 1) throw new InvalidOperationException("Der Overlay-Alert konnte nicht eindeutig persistiert werden.");
    }

    /// <inheritdoc />
    public async Task<OverlayAlertCursor> ReadTailAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default)
    {
        var id = EnsureChannel(channelId);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<TailRow>(new CommandDefinition(TailSql, new { ChannelId = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? OverlayAlertCursor.Start(channelId) : OverlayAlertCursor.Create(channelId, row.CreatedAtUtc, row.AlertId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OverlayAlert>> ReadAfterAsync(OverlayChannelId channelId, OverlayAlertCursor cursor, DateTimeOffset nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var id = EnsureChannel(channelId);
        ArgumentNullException.ThrowIfNull(cursor);
        if (cursor.ChannelId != channelId) throw new ArgumentException("Der Overlay-Cursor gehört nicht zum angefragten Channel.", nameof(cursor));
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take), take, "Die Alert-Lesemenge muss zwischen 1 und 100 liegen.");
        var now = EnsureUtc(nowUtc);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(CleanupSql, new { ChannelId = id, NowUtc = now, CleanupBatchSize = OverlayTransportDefaults.CleanupBatchSize }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AlertRow>(new CommandDefinition(ReadAfterSql, new
        {
            ChannelId = id,
            NowUtc = now,
            ReplaySinceUtc = now.Subtract(OverlayTransportDefaults.ReplayWindow),
            CursorCreatedAtUtc = cursor.CreatedAtUtc,
            CursorAlertId = cursor.AlertId,
            Take = Math.Min(take, OverlayTransportDefaults.MaxBatchSize)
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    private static OverlayAlert Rehydrate(AlertRow row)
    {
        if ((row.SourceType is null) != (row.SourceId is null)) throw new InvalidOperationException("Die persistierte Overlay-SourceReference ist inkonsistent.");
        var source = row.SourceType is null ? null : OverlaySourceReference.Rehydrate(row.SourceType, row.SourceId!);
        return OverlayAlert.Rehydrate(OverlayAlertId.Create(row.Id), OverlayChannelId.Create(row.ChannelId), row.Title, row.Message, row.Variant, row.DurationMilliseconds, source, row.CreatedAtUtc, row.ExpiresAtUtc);
    }

    private static Guid EnsureChannel(OverlayChannelId channelId)
    {
        if (channelId.Value == Guid.Empty) throw new ArgumentException("Die Overlay-Channel-ID darf nicht leer sein.", nameof(channelId));
        return channelId.Value;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMicrosecond != 0) throw new ArgumentException("Der Overlay-Zeitpunkt muss UTC-Mikrosekundenpräzision besitzen.", nameof(value));
        return value;
    }

    private sealed class TailRow
    {
        public DateTimeOffset CreatedAtUtc { get; set; }
        public Guid AlertId { get; set; }
    }

    private sealed class AlertRow
    {
        public Guid Id { get; set; }
        public Guid ChannelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string Variant { get; set; } = string.Empty;
        public int DurationMilliseconds { get; set; }
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
