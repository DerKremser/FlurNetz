using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Lädt ein einzelnes persistiertes Community-Achievement.
/// </summary>
public sealed class GetCommunityAchievement
{
    private readonly ICommunityAchievementStore store;

    /// <summary>
    /// Erstellt den Lookup-Use-Case.
    /// </summary>
    public GetCommunityAchievement(ICommunityAchievementStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert das Achievement oder <see langword="null"/>, wenn es unbekannt ist.
    /// </summary>
    public Task<CommunityAchievement?> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return store.GetAsync(communityIdentityId, achievementDefinitionId, cancellationToken);
    }
}
