using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Lädt eine einzelne Achievement-Definition aus dem internen Katalog.
/// </summary>
public sealed class GetAchievementDefinition
{
    private readonly IAchievementDefinitionStore store;

    /// <summary>
    /// Erstellt den Lookup-Use-Case.
    /// </summary>
    public GetAchievementDefinition(IAchievementDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert die Definition oder <see langword="null"/>, wenn sie unbekannt ist.
    /// </summary>
    public Task<AchievementDefinition?> ExecuteAsync(
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return store.GetAsync(achievementDefinitionId, cancellationToken);
    }
}
