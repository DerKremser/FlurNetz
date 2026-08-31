using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Entfernt die aktuelle Community-Titelauswahl.
/// </summary>
public sealed class ClearCurrentCommunityTitle
{
    private readonly ICommunityTitlesStore store;

    /// <summary>
    /// Erstellt den Use Case mit dem modulbezogenen Store.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Titelzustände.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public ClearCurrentCommunityTitle(ICommunityTitlesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Entfernt die aktuelle Auswahl und liefert, ob eine Auswahl vorhanden war.
    /// </summary>
    public Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            communityIdentityId,
            titles => titles.ClearCurrent(),
            cancellationToken);
    }
}
