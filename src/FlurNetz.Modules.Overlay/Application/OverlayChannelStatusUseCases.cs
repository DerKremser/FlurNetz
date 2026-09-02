using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Aktiviert einen Overlay-Kanal.</summary>
public sealed class EnableOverlayChannel(IOverlayChannelStore store, IClock clock)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));
    /// <summary>Führt die idempotente Aktivierung aus.</summary>
    public Task<OverlayChannel?> ExecuteAsync(OverlayChannelId id, CancellationToken cancellationToken = default) =>
        store.MutateAsync(id, channel => channel.Enable(Canonicalize(clock.UtcNow)), cancellationToken: cancellationToken);
    private static DateTimeOffset Canonicalize(DateTimeOffset value) => value.ToUniversalTime().AddTicks(-(value.UtcDateTime.Ticks % TimeSpan.TicksPerMicrosecond));
}

/// <summary>Deaktiviert einen Overlay-Kanal.</summary>
public sealed class DisableOverlayChannel(IOverlayChannelStore store, IClock clock)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));
    /// <summary>Führt die idempotente Deaktivierung aus.</summary>
    public Task<OverlayChannel?> ExecuteAsync(OverlayChannelId id, CancellationToken cancellationToken = default) =>
        store.MutateAsync(id, channel => channel.Disable(Canonicalize(clock.UtcNow)), cancellationToken: cancellationToken);
    private static DateTimeOffset Canonicalize(DateTimeOffset value) => value.ToUniversalTime().AddTicks(-(value.UtcDateTime.Ticks % TimeSpan.TicksPerMicrosecond));
}

/// <summary>Archiviert einen Overlay-Kanal terminal und invalidiert seinen Source Key.</summary>
public sealed class ArchiveOverlayChannel(IOverlayChannelStore store, IClock clock)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));
    /// <summary>Führt die terminale Archivierung aus.</summary>
    public Task<OverlayChannel?> ExecuteAsync(OverlayChannelId id, CancellationToken cancellationToken = default) =>
        store.MutateAsync(id, channel => channel.Archive(Canonicalize(clock.UtcNow)), invalidateSourceKey: true, cancellationToken: cancellationToken);
    private static DateTimeOffset Canonicalize(DateTimeOffset value) => value.ToUniversalTime().AddTicks(-(value.UtcDateTime.Ticks % TimeSpan.TicksPerMicrosecond));
}
