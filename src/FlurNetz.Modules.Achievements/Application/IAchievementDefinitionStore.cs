using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Definiert die interne Persistenzgrenze für den Achievements-Definitionskatalog.
/// </summary>
/// <remarks>
/// Mutationen erhalten ausschließlich einen synchronen Domain-Callback. Externe asynchrone
/// Arbeit kann dadurch nicht innerhalb der offenen Mutationstransaktion ausgeführt werden.
/// </remarks>
public interface IAchievementDefinitionStore
{
    /// <summary>
    /// Persistiert eine neue Achievement-Definition.
    /// </summary>
    /// <param name="definition">Die bereits gültige Definition.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    Task AddAsync(
        AchievementDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt eine Achievement-Definition oder liefert bei unbekannter ID <see langword="null"/>.
    /// </summary>
    Task<AchievementDefinition?> GetAsync(
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt alle Definitionen in technisch deterministischer ID-Reihenfolge.
    /// </summary>
    Task<IReadOnlyList<AchievementDefinition>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt und mutiert eine Definition atomar über einen synchronen Domain-Callback.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        AchievementDefinitionId achievementDefinitionId,
        Func<AchievementDefinition, TResult> operation,
        CancellationToken cancellationToken = default);
}
