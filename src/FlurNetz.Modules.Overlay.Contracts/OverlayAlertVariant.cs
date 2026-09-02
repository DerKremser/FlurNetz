namespace FlurNetz.Modules.Overlay.Contracts;

/// <summary>Die exakt unterstützten Overlay-V1-Alert-Varianten.</summary>
public static class OverlayAlertVariant
{
    /// <summary>Neutrale Standarddarstellung.</summary>
    public const string Default = "default";

    /// <summary>Erfolgsdarstellung.</summary>
    public const string Success = "success";

    /// <summary>Warnungsdarstellung.</summary>
    public const string Warning = "warning";

    /// <summary>Feierdarstellung.</summary>
    public const string Celebration = "celebration";

    /// <summary>Prüft eine V1-Variante.</summary>
    public static bool IsSupported(string? value) => value is Default or Success or Warning or Celebration;
}
