using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Lädt eine einzelne Title-Definition aus dem internen Katalog.
/// </summary>
public sealed class GetTitleDefinition
{
    private readonly ITitleDefinitionStore store;

    /// <summary>
    /// Erstellt den Lookup-Use-Case.
    /// </summary>
    public GetTitleDefinition(ITitleDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Liefert die Definition oder <see langword="null"/>, wenn sie unbekannt ist.
    /// </summary>
    public Task<TitleDefinition?> ExecuteAsync(
        TitleDefinitionId titleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return store.GetAsync(titleDefinitionId, cancellationToken);
    }
}
