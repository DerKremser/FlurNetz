using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Opaker, channelgebundener Weiterlesepunkt in Alert-Reihenfolge.</summary>
public sealed record OverlayAlertCursor
{
    private OverlayAlertCursor(OverlayChannelId channelId, DateTimeOffset createdAtUtc, Guid alertId)
    {
        ChannelId = channelId;
        CreatedAtUtc = createdAtUtc;
        AlertId = alertId;
    }

    /// <summary>Zugehöriger Channel.</summary>
    public OverlayChannelId ChannelId { get; }

    /// <summary>Alert-Erstellungszeitpunkt des Cursors.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Alert-ID als zweiter Sortierschlüssel.</summary>
    public Guid AlertId { get; }

    /// <summary>Erstellt einen validierten Cursor.</summary>
    public static OverlayAlertCursor Create(OverlayChannelId channelId, DateTimeOffset createdAtUtc, Guid alertId)
    {
        if (channelId.Value == Guid.Empty) throw new ArgumentException("Der Cursor benötigt einen Channel.", nameof(channelId));
        if (createdAtUtc.Offset != TimeSpan.Zero || createdAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException("Der Cursor-Zeitpunkt muss UTC-Mikrosekundenpräzision besitzen.", nameof(createdAtUtc));
        }

        if (alertId == Guid.Empty && createdAtUtc != DateTimeOffset.MinValue)
        {
            throw new ArgumentException("Ein nicht leerer Cursor benötigt eine Alert-ID.", nameof(alertId));
        }

        return new OverlayAlertCursor(channelId, createdAtUtc, alertId);
    }

    /// <summary>Erzeugt den Anfangscursor für einen leeren Channel.</summary>
    public static OverlayAlertCursor Start(OverlayChannelId channelId) =>
        Create(channelId, DateTimeOffset.MinValue, Guid.Empty);
}
