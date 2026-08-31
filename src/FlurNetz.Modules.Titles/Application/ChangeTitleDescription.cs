using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Ändert oder entfernt die Beschreibung einer vorhandenen Title-Definition.
/// </summary>
public sealed class ChangeTitleDescription
{
    private readonly ITitleDefinitionStore store;

    /// <summary>
    /// Erstellt den Beschreibungs-Use-Case.
    /// </summary>
    public ChangeTitleDescription(ITitleDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Beschreibungsänderung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        TitleDefinitionId titleDefinitionId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            titleDefinitionId,
            definition => definition.ChangeDescription(description),
            cancellationToken);
    }
}
