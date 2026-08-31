using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Setzt einen freigeschalteten Titel als aktuelle Community-Auswahl.
/// </summary>
public sealed class SetCurrentCommunityTitle
{
    private readonly ICommunityTitlesStore store;

    /// <summary>
    /// Erstellt den Use Case mit dem modulbezogenen Store.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Titelzustände.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public SetCurrentCommunityTitle(ICommunityTitlesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Setzt den aktuellen Titel und liefert, ob sich die Auswahl geändert hat.
    /// </summary>
    /// <exception cref="TitleNotUnlockedException">Wenn der Titel nicht freigeschaltet ist.</exception>
    public Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        TitleDefinitionId titleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteAsync(
            communityIdentityId,
            titles => titles.SetCurrent(titleDefinitionId),
            cancellationToken);
    }
}
