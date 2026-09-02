namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Implementation-owned Identität eines persistierten Overlay-Alerts.</summary>
public readonly record struct OverlayAlertId
{
    private readonly Guid value;

    private OverlayAlertId(Guid value) => this.value = value;

    /// <summary>Liefert den GUID-Wert.</summary>
    public Guid Value => value;

    /// <summary>Erstellt eine nicht leere Alert-ID.</summary>
    public static OverlayAlertId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Overlay-Alert-ID darf nicht leer sein.", nameof(value));
        }

        return new OverlayAlertId(value);
    }

    /// <summary>Erzeugt eine neue Alert-ID.</summary>
    public static OverlayAlertId New() => Create(Guid.NewGuid());
}
