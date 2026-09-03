using System.Data.Common;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Definiert die interne Persistenzgrenze für permanente Community-Achievements.
/// </summary>
public interface ICommunityAchievementStore
{
    /// <summary>
    /// Fügt ein Achievement atomar und idempotent hinzu.
    /// </summary>
    /// <param name="achievement">Das bereits gültige Achievement mit seinem Unlock-Zeitpunkt.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns><see langword="true"/> beim ersten erfolgreichen Insert, sonst <see langword="false"/>.</returns>
    Task<bool> UnlockAsync(
        CommunityAchievement achievement,
        CancellationToken cancellationToken = default);

    Task<bool> UnlockAsync(
        CommunityAchievement achievement,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");

    /// <summary>
    /// Lädt ein Achievement über den zusammengesetzten Community-Schlüssel.
    /// </summary>
    Task<CommunityAchievement?> GetAsync(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt alle Achievements einer Community in deterministischer Reihenfolge.
    /// </summary>
    Task<IReadOnlyList<CommunityAchievement>> ListAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);
}
