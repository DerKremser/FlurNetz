using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Lädt den vollständigen internen Achievements-Definitionskatalog.
/// </summary>
public sealed class ListAchievementDefinitions
{
    private readonly IAchievementDefinitionStore store;

    /// <summary>
    /// Erstellt den List-Use-Case.
    /// </summary>
    public ListAchievementDefinitions(IAchievementDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert den Katalog in technisch deterministischer Reihenfolge.
    /// </summary>
    public Task<IReadOnlyList<AchievementDefinition>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(cancellationToken);
    }
}
