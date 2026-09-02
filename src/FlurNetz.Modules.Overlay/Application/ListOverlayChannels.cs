using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Listet Overlay-Kanäle.</summary>
public sealed class ListOverlayChannels(IOverlayChannelStore store)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Lädt alle Kanäle.</summary>
    public Task<IReadOnlyList<OverlayChannel>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);
}
