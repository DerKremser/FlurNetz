using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Der angeforderte Overlay-Kanal wurde nicht gefunden.</summary>
public sealed class OverlayChannelNotFoundException(OverlayChannelId channelId)
    : Exception($"Der Overlay-Kanal '{channelId.Value}' wurde nicht gefunden.")
{
    /// <summary>Betroffene Kanal-ID.</summary>
    public OverlayChannelId ChannelId { get; } = channelId;
}

/// <summary>Eine Mutation ist für einen archivierten Kanal nicht zulässig.</summary>
public sealed class OverlayChannelArchivedException(OverlayChannelId channelId)
    : Exception($"Der Overlay-Kanal '{channelId.Value}' ist archiviert und kann nicht mehr geändert werden.")
{
    /// <summary>Betroffene Kanal-ID.</summary>
    public OverlayChannelId ChannelId { get; } = channelId;
}
