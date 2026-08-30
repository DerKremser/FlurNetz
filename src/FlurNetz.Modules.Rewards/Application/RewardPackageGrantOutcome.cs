namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Beschreibt das Ergebnis eines idempotenten Package-Grant-Versuchs.
/// </summary>
public enum RewardPackageGrantOutcome
{
    /// <summary>
    /// Alle Definitionen des Packages wurden erstmals atomar ausgeführt.
    /// </summary>
    Granted,

    /// <summary>
    /// Alle Definitionen waren für die Quelle bereits erfolgreich ausgeführt.
    /// </summary>
    AlreadyGranted
}
