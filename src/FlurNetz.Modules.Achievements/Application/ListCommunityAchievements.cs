using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Lädt die permanenten Achievements einer Community.
/// </summary>
public sealed class ListCommunityAchievements
{
    private readonly ICommunityAchievementStore store;

    /// <summary>
    /// Erstellt den List-Use-Case.
    /// </summary>
    public ListCommunityAchievements(ICommunityAchievementStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert eine niemals null-fähige Liste in der vom Store definierten Reihenfolge.
    /// </summary>
    public Task<IReadOnlyList<CommunityAchievement>> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(communityIdentityId, cancellationToken);
    }
}
