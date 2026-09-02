namespace FlurNetz.Modules.Overlay.Contracts;

/// <summary>Gemeinsame numerische V1-Grenzen für Alert-Anzeigezeiten.</summary>
public static class OverlayAlertDurationRules
{
    /// <summary>Minimale Anzeigezeit.</summary>
    public const int MinimumMilliseconds = 1_000;
    /// <summary>Default-Anzeigezeit.</summary>
    public const int DefaultMilliseconds = 5_000;
    /// <summary>Maximale Anzeigezeit.</summary>
    public const int MaximumMilliseconds = 30_000;
}
