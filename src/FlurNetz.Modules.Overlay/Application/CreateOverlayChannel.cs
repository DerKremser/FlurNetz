using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Use Case zum Erzeugen eines Overlay-Kanals und einmaligen Source Keys.</summary>
public sealed class CreateOverlayChannel
{
    private readonly IOverlayChannelStore store;
    private readonly IClock clock;

    /// <summary>Erstellt den Use Case.</summary>
    public CreateOverlayChannel(IOverlayChannelStore store, IClock clock)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Erzeugt den deaktivierten Kanal.</summary>
    public async Task<OverlayChannelSecret> ExecuteAsync(string displayName, string? description, CancellationToken cancellationToken = default)
    {
        var channel = OverlayChannel.Create(OverlayChannelId.New(), displayName, description, Canonicalize(clock.UtcNow));
        var sourceKey = OverlaySourceKey.Generate();
        await store.AddAsync(channel, OverlaySourceKey.Hash(sourceKey), cancellationToken).ConfigureAwait(false);
        return new OverlayChannelSecret(channel, sourceKey);
    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
