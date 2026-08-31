using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Ändert oder entfernt die Beschreibung einer Achievement-Definition.
/// </summary>
public sealed class ChangeAchievementDescription
{
    private readonly IAchievementDefinitionStore store;

    /// <summary>
    /// Erstellt den Beschreibungs-Use-Case.
    /// </summary>
    public ChangeAchievementDescription(IAchievementDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Beschreibungsänderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        AchievementDefinitionId achievementDefinitionId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            achievementDefinitionId,
            definition => definition.ChangeDescription(description),
            cancellationToken);
    }
}
