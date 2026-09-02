using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Ändert Name und Beschreibung eines nicht archivierten Kanals.</summary>
public sealed class UpdateOverlayChannelMetadata(IOverlayChannelStore store, IClock clock)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Führt die Mutation aus.</summary>
    public async Task<OverlayChannel?> ExecuteAsync(OverlayChannelId channelId, string displayName, string? description, CancellationToken cancellationToken = default) =>
        await store.MutateAsync(channelId, channel => channel.UpdateMetadata(displayName, description, Canonicalize(clock.UtcNow)), cancellationToken: cancellationToken).ConfigureAwait(false);

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
