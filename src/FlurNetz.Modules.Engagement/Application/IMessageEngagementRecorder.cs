using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Engagement.Domain;

namespace FlurNetz.Modules.Engagement.Application;

/// <summary>
/// Definiert die atomare Persistenzgrenze für eine Message-Aktivität und ihre Outbox-Nachricht.
/// </summary>
/// <remarks>
/// Der Use Case kennt dadurch weder SQL noch Transaktionsdetails. Der Port garantiert, dass
/// der Engagement-Datensatz und das Integration Event gemeinsam committed oder gemeinsam
/// zurückgerollt werden; ein Publish nach einem separaten Activity-Commit ist ausgeschlossen.
/// </remarks>
public interface IMessageEngagementRecorder
{
    /// <summary>
    /// Persistiert Aktivität und zugehörigen Outbox-Envelope in einer gemeinsamen Transaktion.
    /// </summary>
    /// <param name="activity">Die bereits erzeugte normalisierte Message-Aktivität.</param>
    /// <param name="envelope">Das fachliche Event mit stabilem Message-Envelope.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Eine Aufgabe nach erfolgreichem gemeinsamen Commit.</returns>
    Task RecordAsync(
        EngagementActivity activity,
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default);
}
