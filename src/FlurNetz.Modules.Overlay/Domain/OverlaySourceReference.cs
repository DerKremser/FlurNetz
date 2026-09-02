namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Optionale, vollständig spezifizierte Quelle eines Alerts.</summary>
public sealed record OverlaySourceReference
{
    private OverlaySourceReference(string sourceType, string sourceId)
    {
        SourceType = sourceType;
        SourceId = sourceId;
    }

    /// <summary>Kanontischer Quelltyp.</summary>
    public string SourceType { get; }

    /// <summary>Kanonische Quellidentität.</summary>
    public string SourceId { get; }

    /// <summary>Erstellt und validiert eine vollständige Quellenreferenz.</summary>
    public static OverlaySourceReference Create(string sourceType, string sourceId) =>
        new(
            OverlayText.Required(sourceType, nameof(sourceType), "Der Overlay-SourceType", 100),
            OverlayText.Required(sourceId, nameof(sourceId), "Die Overlay-SourceId", 200));

    /// <summary>Rehydriert bereits kanonische Quellwerte.</summary>
    public static OverlaySourceReference Rehydrate(string sourceType, string sourceId)
    {
        OverlayText.EnsureCanonical(sourceType, nameof(sourceType), "der Overlay-SourceType", 100, false);
        OverlayText.EnsureCanonical(sourceId, nameof(sourceId), "die Overlay-SourceId", 200, false);
        return new OverlaySourceReference(sourceType, sourceId);
    }
}
