using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Benennt eine vorhandene Achievement-Definition um.
/// </summary>
public sealed class RenameAchievementDefinition
{
    private readonly IAchievementDefinitionStore store;

    /// <summary>
    /// Erstellt den Rename-Use-Case.
    /// </summary>
    public RenameAchievementDefinition(IAchievementDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Umbenennung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        AchievementDefinitionId achievementDefinitionId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            achievementDefinitionId,
            definition => definition.Rename(displayName),
            cancellationToken);
    }
}
