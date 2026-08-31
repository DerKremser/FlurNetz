using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Lädt den vollständigen internen Titles-Definitionskatalog.
/// </summary>
public sealed class ListTitleDefinitions
{
    private readonly ITitleDefinitionStore store;

    /// <summary>
    /// Erstellt den List-Use-Case.
    /// </summary>
    public ListTitleDefinitions(ITitleDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert den Katalog in der vom Store definierten technisch deterministischen Reihenfolge.
    /// </summary>
    public Task<IReadOnlyList<TitleDefinition>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(cancellationToken);
    }
}
