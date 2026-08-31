using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Benennt eine vorhandene Title-Definition über ihre Domain-Mutation um.
/// </summary>
public sealed class RenameTitleDefinition
{
    private readonly ITitleDefinitionStore store;

    /// <summary>
    /// Erstellt den Rename-Use-Case.
    /// </summary>
    public RenameTitleDefinition(ITitleDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt die fachliche Umbenennung atomar aus.
    /// </summary>
    public Task<bool> ExecuteAsync(
        TitleDefinitionId titleDefinitionId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            titleDefinitionId,
            definition => definition.Rename(displayName),
            cancellationToken);
    }
}
