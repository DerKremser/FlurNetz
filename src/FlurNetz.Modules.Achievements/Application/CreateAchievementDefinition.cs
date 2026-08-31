using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Erzeugt und persistiert eine neue Achievement-Definition.
/// </summary>
public sealed class CreateAchievementDefinition
{
    private readonly IAchievementDefinitionStore store;

    /// <summary>
    /// Erstellt den Katalog-Create-Use-Case.
    /// </summary>
    public CreateAchievementDefinition(IAchievementDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Vergibt eine neue ID, validiert die Definition und persistiert sie.
    /// </summary>
    /// <returns>Die erzeugte und persistierte Definition.</returns>
    public async Task<AchievementDefinition> ExecuteAsync(
        string displayName,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var definition = AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            displayName,
            description);

        await store.AddAsync(definition, cancellationToken).ConfigureAwait(false);
        return definition;
    }
}
