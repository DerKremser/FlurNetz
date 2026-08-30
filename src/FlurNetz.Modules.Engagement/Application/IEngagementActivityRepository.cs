using FlurNetz.Modules.Engagement.Domain;

namespace FlurNetz.Modules.Engagement.Application;

/// <summary>
/// Definiert den gezielten Persistenz-Port für Engagement-Aktivitäten.
/// </summary>
/// <remarks>
/// Der Port kennt nur die für den aktuellen Recording-Slice benötigten Operationen. Er prüft
/// nicht, ob die Community-Identität in Identity existiert; diese Auflösung ist vor Engagement
/// bereits abgeschlossen und bleibt in der Verantwortung des vorgelagerten Flows.
/// </remarks>
public interface IEngagementActivityRepository
{
    /// <summary>
    /// Speichert eine gültige Engagement-Aktivität und schließt deren Transaktion ab.
    /// </summary>
    /// <param name="activity">Die zu speichernde Message-Aktivität.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Ein Task nach erfolgreichem Commit.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="activity"/> fehlt.</exception>
    Task AddAsync(EngagementActivity activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt eine Engagement-Aktivität über ihre interne Kennung.
    /// </summary>
    /// <param name="id">Die gültige Kennung der gesuchten Aktivität.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Die Aktivität oder <see langword="null"/>, wenn keine Zeile existiert.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="id"/> leer ist.</exception>
    Task<EngagementActivity?> GetByIdAsync(
        EngagementActivityId id,
        CancellationToken cancellationToken = default);
}
