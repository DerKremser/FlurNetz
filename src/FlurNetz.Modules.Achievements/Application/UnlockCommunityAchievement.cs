using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Schaltet ein Achievement dauerhaft und idempotent für eine Community frei.
/// </summary>
public sealed class UnlockCommunityAchievement
{
    private readonly IAchievementDefinitionStore definitionStore;
    private readonly ICommunityAchievementStore achievementStore;
    private readonly IClock clock;

    /// <summary>
    /// Erstellt den Unlock-Use-Case.
    /// </summary>
    /// <param name="definitionStore">Der eigene Achievement-Definitionskatalog.</param>
    /// <param name="achievementStore">Der persistente Community-Achievement-Store.</param>
    /// <param name="clock">Die UTC-Zeitquelle für den ersten erfolgreichen Unlock.</param>
    public UnlockCommunityAchievement(
        IAchievementDefinitionStore definitionStore,
        ICommunityAchievementStore achievementStore,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(definitionStore);
        ArgumentNullException.ThrowIfNull(achievementStore);
        ArgumentNullException.ThrowIfNull(clock);
        this.definitionStore = definitionStore;
        this.achievementStore = achievementStore;
        this.clock = clock;
    }

    /// <summary>
    /// Prüft die Definition, erzeugt das UTC-Domainobjekt und übergibt es dem atomaren Store.
    /// </summary>
    public async Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validAchievementDefinitionId = AchievementDefinitionId.Create(achievementDefinitionId.Value);

        var definition = await definitionStore
            .GetAsync(validAchievementDefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            throw new AchievementDefinitionNotFoundException(validAchievementDefinitionId);
        }

        var achievement = CommunityAchievement.Create(
            validCommunityIdentityId,
            validAchievementDefinitionId,
            clock.UtcNow);

        return await achievementStore
            .UnlockAsync(achievement, cancellationToken)
            .ConfigureAwait(false);
    }
}
