using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Entfernt eine Community-Titelberechtigung über die atomare Titles-Persistenzgrenze.
/// </summary>
public sealed class LockCommunityTitle
{
    private readonly ICommunityTitlesStore store;

    /// <summary>
    /// Erstellt den Use Case mit dem modulbezogenen Store.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Titelzustände.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public LockCommunityTitle(ICommunityTitlesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Sperrt den Titel und liefert, ob sich die Berechtigungsmenge geändert hat.
    /// </summary>
    public Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        TitleDefinitionId titleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            communityIdentityId,
            titles => titles.Lock(titleDefinitionId),
            cancellationToken);
    }
}
