using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Unveränderlicher Snapshot eines Overlay-Alerts.</summary>
public sealed record OverlayAlert
{
    private OverlayAlert(
        OverlayAlertId id,
        OverlayChannelId channelId,
        string title,
        string? message,
        string variant,
        OverlayAlertDuration duration,
        OverlaySourceReference? sourceReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        Title = title;
        Message = message;
        Variant = variant;
        Duration = duration;
        SourceReference = sourceReference;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Alert-ID.</summary>
    public OverlayAlertId Id { get; }

    /// <summary>Alias für explizite Aufrufer.</summary>
    public OverlayAlertId OverlayAlertId => Id;

    /// <summary>Zielkanal.</summary>
    public OverlayChannelId ChannelId { get; }

    /// <summary>Expliziter Alias des Zielkanals.</summary>
    public OverlayChannelId OverlayChannelId => ChannelId;

    /// <summary>Alert-Titel.</summary>
    public string Title { get; }

    /// <summary>Optionale Alert-Nachricht.</summary>
    public string? Message { get; }

    /// <summary>V1-Variante.</summary>
    public string Variant { get; }

    /// <summary>Invariantengesicherte Anzeigezeit.</summary>
    public OverlayAlertDuration Duration { get; }

    /// <summary>Anzeigezeit in Millisekunden.</summary>
    public int DurationMilliseconds => Duration.Milliseconds;

    /// <summary>Optionale vollständige Quelle.</summary>
    public OverlaySourceReference? SourceReference { get; }

    /// <summary>Erstellungszeitpunkt.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Ablaufzeitpunkt.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    /// <summary>Erstellt einen neuen unveränderlichen Alert.</summary>
    public static OverlayAlert Create(
        OverlayAlertId id,
        OverlayChannelId channelId,
        string title,
        string? message,
        string variant,
        int durationMilliseconds,
        OverlaySourceReference? sourceReference,
        DateTimeOffset createdAtUtc)
    {
        EnsureIds(id, channelId);
        var created = OverlayTimestamp.Ensure(createdAtUtc, nameof(createdAtUtc));
        var duration = OverlayAlertDuration.Create(durationMilliseconds);
        var expires = OverlayTimestamp.Ensure(created.AddMilliseconds(duration.Milliseconds), nameof(createdAtUtc));
        return new OverlayAlert(
            id,
            channelId,
            OverlayText.Required(title, nameof(title), "Der Overlay-Alert-Titel", 200),
            OverlayText.Optional(message, nameof(message), "Die Overlay-Alert-Nachricht", 2_000),
            EnsureVariant(variant),
            duration,
            sourceReference,
            created,
            expires);
    }

    /// <summary>Rehydriert einen Snapshot und akzeptiert keine stillen Korrekturen.</summary>
    public static OverlayAlert Rehydrate(
        OverlayAlertId id,
        OverlayChannelId channelId,
        string title,
        string? message,
        string variant,
        int durationMilliseconds,
        OverlaySourceReference? sourceReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        EnsureIds(id, channelId);
        OverlayText.EnsureCanonical(title, nameof(title), "der Overlay-Alert-Titel", 200, false);
        OverlayText.EnsureCanonical(message, nameof(message), "die Overlay-Alert-Nachricht", 2_000, true);
        var duration = OverlayAlertDuration.Create(durationMilliseconds);
        var created = OverlayTimestamp.Ensure(createdAtUtc, nameof(createdAtUtc));
        var expires = OverlayTimestamp.Ensure(expiresAtUtc, nameof(expiresAtUtc));
        if (expires <= created || expires != created.AddMilliseconds(duration.Milliseconds))
        {
            throw new ArgumentException("ExpiresAtUtc muss exakt nach der Alert-Dauer auf CreatedAtUtc folgen.", nameof(expiresAtUtc));
        }

        return new OverlayAlert(id, channelId, title, message, EnsureVariant(variant), duration, sourceReference, created, expires);
    }

    private static string EnsureVariant(string? variant)
    {
        if (!OverlayAlertVariant.IsSupported(variant))
        {
            throw new ArgumentException("Die Overlay-Alert-Variante ist für V1 nicht unterstützt.", nameof(variant));
        }

        return variant!;
    }

    private static void EnsureIds(OverlayAlertId id, OverlayChannelId channelId)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Ein Overlay-Alert benötigt eine nicht leere ID.", nameof(id));
        if (channelId.Value == Guid.Empty) throw new ArgumentException("Ein Overlay-Alert benötigt einen nicht leeren Kanal.", nameof(channelId));
    }
}
