namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Gemeinsame, bewusst kleine V1-Grenzen für den Overlay-Transport.</summary>
public static class OverlayTransportDefaults
{
    /// <summary>Begrenzt, wie weit ein Reconnect in der Alert-Historie zurücklesen darf.</summary>
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(2);

    /// <summary>Polling-Intervall des SSE-Endpoints.</summary>
    public const int PollIntervalMilliseconds = 500;

    /// <summary>Intervall für SSE-Kommentar-Heartbeats.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>Maximale Alert-Anzahl je Polling-Runde.</summary>
    public const int MaxBatchSize = 100;

    /// <summary>Maximale Anzahl opportunistisch zu löschender abgelaufener Alerts.</summary>
    public const int CleanupBatchSize = 50;
}
