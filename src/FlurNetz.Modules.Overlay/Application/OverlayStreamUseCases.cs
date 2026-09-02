using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Löst eine nicht archivierte Browser Source auf und bildet ihren Startcursor.</summary>
public sealed class ResolveBrowserSource(IOverlayChannelStore channelStore, IOverlayAlertStore alertStore)
{
    private readonly IOverlayChannelStore channelStore = channelStore ?? throw new ArgumentNullException(nameof(channelStore));
    private readonly IOverlayAlertStore alertStore = alertStore ?? throw new ArgumentNullException(nameof(alertStore));

    /// <summary>Löst den Source Key auf.</summary>
    public async Task<OverlayBrowserSourceResolution?> ExecuteAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        var channel = await channelStore.ResolveBySourceKeyAsync(sourceKey, cancellationToken).ConfigureAwait(false);
        if (channel is null) return null;
        var tail = await alertStore.ReadTailAsync(channel.Id, cancellationToken).ConfigureAwait(false);
        return new OverlayBrowserSourceResolution(channel, tail);
    }
}

/// <summary>Liest den Tail-Cursor eines validierten Channels.</summary>
public sealed class ReadStreamTail(IOverlayAlertStore alertStore)
{
    private readonly IOverlayAlertStore alertStore = alertStore ?? throw new ArgumentNullException(nameof(alertStore));
    /// <summary>Liest den aktuellen Tail.</summary>
    public Task<OverlayAlertCursor> ExecuteAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default) =>
        alertStore.ReadTailAsync(channelId, cancellationToken);
}

/// <summary>Liest gültige Alerts nach einem gebundenen Cursor.</summary>
public sealed class ReadAlertsAfterCursor(IOverlayAlertStore alertStore)
{
    private readonly IOverlayAlertStore alertStore = alertStore ?? throw new ArgumentNullException(nameof(alertStore));
    /// <summary>Liest maximal die angeforderte Anzahl.</summary>
    public Task<IReadOnlyList<OverlayAlert>> ExecuteAsync(OverlayChannelId channelId, OverlayAlertCursor cursor, DateTimeOffset nowUtc, int take = 100, CancellationToken cancellationToken = default) =>
        alertStore.ReadAfterAsync(channelId, cursor, nowUtc, take, cancellationToken);
}
