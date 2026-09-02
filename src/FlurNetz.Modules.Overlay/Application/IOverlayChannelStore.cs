using System.Data.Common;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Persistenzgrenze für Overlay-Kanäle und technische Source-Key-Zustände.</summary>
public interface IOverlayChannelStore
{
    /// <summary>Fügt einen neuen Kanal samt gehashtem Source Key ein.</summary>
    Task AddAsync(OverlayChannel channel, string sourceKeyHash, CancellationToken cancellationToken = default);

    /// <summary>Lädt einen Kanal.</summary>
    Task<OverlayChannel?> GetAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default);

    /// <summary>Lädt alle Kanäle deterministisch.</summary>
    Task<IReadOnlyList<OverlayChannel>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Lädt einen Kanal mit Row-Lock innerhalb der übergebenen Transaktion.</summary>
    Task<OverlayChannel?> GetForUpdateAsync(
        OverlayChannelId channelId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Lädt einen nicht archivierten Kanal anhand des gehashten Source Keys.</summary>
    Task<OverlayChannel?> ResolveBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default);

    /// <summary>Mutiert einen Kanal unter PostgreSQL-Row-Lock.</summary>
    Task<OverlayChannel?> MutateAsync(
        OverlayChannelId channelId,
        Func<OverlayChannel, bool> mutation,
        string? replacementSourceKeyHash = null,
        bool invalidateSourceKey = false,
        CancellationToken cancellationToken = default);
}
