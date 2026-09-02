namespace FlurNetz.Modules.Overlay.Contracts;

/// <summary>Stabile Identität eines Overlay-Kanals.</summary>
public readonly record struct OverlayChannelId
{
    private readonly Guid value;

    private OverlayChannelId(Guid value) => this.value = value;

    /// <summary>Liefert den GUID-Wert.</summary>
    public Guid Value => value;

    /// <summary>Erstellt eine nicht leere Kanal-ID.</summary>
    public static OverlayChannelId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Overlay-Channel-ID darf nicht leer sein.", nameof(value));
        }

        return new OverlayChannelId(value);
    }

    /// <summary>Erzeugt eine neue Kanal-ID.</summary>
    public static OverlayChannelId New() => Create(Guid.NewGuid());
}
