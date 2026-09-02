using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using System.Data.Common;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Gezielte Persistenzgrenze für unveränderliche Overlay-Alerts.</summary>
public interface IOverlayAlertStore
{
    /// <summary>Persistiert einen Alert in einer eigenen Management-Transaktion.</summary>
    Task AddAsync(OverlayAlert alert, CancellationToken cancellationToken = default);

    /// <summary>Persistiert einen Alert in der Transaktion des Aufrufers.</summary>
    Task AddAsync(OverlayAlert alert, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Liest den aktuellen Tail-Cursor eines Channels.</summary>
    Task<OverlayAlertCursor> ReadTailAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default);

    /// <summary>Liest noch gültige Alerts strikt nach dem Cursor.</summary>
    Task<IReadOnlyList<OverlayAlert>> ReadAfterAsync(
        OverlayChannelId channelId,
        OverlayAlertCursor cursor,
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default);
}
