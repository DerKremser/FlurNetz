using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Erzeugt und persistiert eine neue Title-Definition.
/// </summary>
public sealed class CreateTitleDefinition
{
    private readonly ITitleDefinitionStore store;

    /// <summary>
    /// Erstellt den Katalog-Create-Use-Case.
    /// </summary>
    public CreateTitleDefinition(ITitleDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Erzeugt eine neue ID, validiert die Domain-Definition und persistiert sie.
    /// </summary>
    public async Task<TitleDefinitionId> ExecuteAsync(
        string displayName,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            displayName,
            description);

        await store.AddAsync(definition, cancellationToken).ConfigureAwait(false);
        return definition.Id;
    }
}
