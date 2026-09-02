namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Invariantengesicherte Anzeigezeit eines Overlay-Alerts.</summary>
public readonly record struct OverlayAlertDuration
{
    /// <summary>Minimale Anzeigezeit.</summary>
    public const int MinimumMilliseconds = 1_000;

    /// <summary>Standardanzeigezeit.</summary>
    public const int DefaultMilliseconds = 5_000;

    /// <summary>Maximale Anzeigezeit.</summary>
    public const int MaximumMilliseconds = 30_000;

    private OverlayAlertDuration(int milliseconds) => Milliseconds = milliseconds;

    /// <summary>Anzeigezeit in Millisekunden.</summary>
    public int Milliseconds { get; }

    /// <summary>Erstellt eine gültige Anzeigezeit.</summary>
    public static OverlayAlertDuration Create(int milliseconds)
    {
        if (milliseconds is < MinimumMilliseconds or > MaximumMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds,
                $"Die Alert-Dauer muss zwischen {MinimumMilliseconds} und {MaximumMilliseconds} Millisekunden liegen.");
        }

        return new OverlayAlertDuration(milliseconds);
    }

    /// <summary>Die V1-Standardanzeigezeit.</summary>
    public static OverlayAlertDuration Default => Create(DefaultMilliseconds);
}
