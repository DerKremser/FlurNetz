using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Lädt einen einzelnen Overlay-Kanal.</summary>
public sealed class GetOverlayChannel(IOverlayChannelStore store)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Lädt den Kanal oder null.</summary>
    public Task<OverlayChannel?> ExecuteAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default) =>
        store.GetAsync(channelId, cancellationToken);
}
