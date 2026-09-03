using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Persistence.Transactions;
using FlurNetz.Persistence.Connections;
using System.Data.Common;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Implementiert die transaction-aware Automation-Publish-Capability.</summary>
public sealed class OverlayAlertPublishCapability(
    IOverlayChannelStore channelStore,
    IOverlayAlertStore alertStore,
    IClock clock) : IOverlayAlertPublish
{
    private readonly IOverlayChannelStore channelStore = channelStore ?? throw new ArgumentNullException(nameof(channelStore));
    private readonly IOverlayAlertStore alertStore = alertStore ?? throw new ArgumentNullException(nameof(alertStore));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public async Task<OverlayAlertPublishResult> PublishAsync(OverlayAlertPublishRequest request, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var channel = await channelStore.GetForUpdateAsync(request.ChannelId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (channel is null) return new(OverlayAlertPublishStatus.ChannelNotFound);
        if (channel.IsArchived) return new(OverlayAlertPublishStatus.ChannelArchived);
        if (!channel.IsEnabled) return new(OverlayAlertPublishStatus.ChannelDisabled);

        var alert = CreateAlert(request);
        await alertStore.AddAsync(alert, connection, transaction, cancellationToken).ConfigureAwait(false);
        return new(OverlayAlertPublishStatus.Published, alert.Id.Value);
    }

    private OverlayAlert CreateAlert(OverlayAlertPublishRequest request) =>
        OverlayAlert.Create(
            OverlayAlertId.New(),
            request.ChannelId,
            request.Title,
            request.Message,
            request.Variant,
            request.DurationMilliseconds,
            CreateSource(request.SourceType, request.SourceId),
            Canonicalize(clock.UtcNow));

    private static OverlaySourceReference? CreateSource(string? sourceType, string? sourceId) =>
        sourceType is null && sourceId is null ? null : OverlaySourceReference.Create(sourceType!, sourceId!);

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}

/// <summary>Erzeugt normale Alerts in einer eigenen Transaktion.</summary>
public sealed class PublishOverlayAlert(IPostgreSqlConnectionFactory connectionFactory, IOverlayAlertPublish publisher)
{
    private readonly IPostgreSqlConnectionFactory connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly IOverlayAlertPublish publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));

    /// <summary>Publiziert oder unterdrückt den Alert.</summary>
    public async Task<OverlayAlertPublishResult> ExecuteAsync(OverlayAlertPublishRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await publisher.PublishAsync(request, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>Erzeugt Preview-Alerts auch auf deaktivierten, aber nicht archivierten Kanälen.</summary>
public sealed class PublishPreviewAlert(
    IPostgreSqlConnectionFactory connectionFactory,
    IOverlayChannelStore channelStore,
    IOverlayAlertStore alertStore,
    IClock clock)
{
    private readonly IPostgreSqlConnectionFactory connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly IOverlayChannelStore channelStore = channelStore ?? throw new ArgumentNullException(nameof(channelStore));
    private readonly IOverlayAlertStore alertStore = alertStore ?? throw new ArgumentNullException(nameof(alertStore));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Publiziert einen Preview-Alert oder liefert einen Suppression-Status.</summary>
    public async Task<OverlayAlertPublishResult> ExecuteAsync(OverlayAlertPublishRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await ExecuteAsync(request, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task<OverlayAlertPublishResult> ExecuteAsync(
        OverlayAlertPublishRequest request,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return ExecuteWithinTransactionAsync(request, connection, transaction, cancellationToken);
    }

    private async Task<OverlayAlertPublishResult> ExecuteWithinTransactionAsync(
        OverlayAlertPublishRequest request,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var channel = await channelStore.GetForUpdateAsync(request.ChannelId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (channel is null) return new(OverlayAlertPublishStatus.ChannelNotFound);
        if (channel.IsArchived) return new(OverlayAlertPublishStatus.ChannelArchived);
        var source = request.SourceType is null && request.SourceId is null ? null : OverlaySourceReference.Create(request.SourceType!, request.SourceId!);
        var alert = OverlayAlert.Create(OverlayAlertId.New(), request.ChannelId, request.Title, request.Message, request.Variant, request.DurationMilliseconds, source, Canonicalize(clock.UtcNow));
        await alertStore.AddAsync(alert, connection, transaction, cancellationToken).ConfigureAwait(false);
        return new(OverlayAlertPublishStatus.Published, alert.Id.Value);
    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
