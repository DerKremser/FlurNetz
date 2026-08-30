using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Titles.Domain;

/// <summary>
/// Hält die freigeschalteten Titel und die optionale aktuelle Auswahl einer Community-Identität.
/// </summary>
/// <remarks>
/// Eine Community-Identität kann beliebig viele unterschiedliche Titel freigeschaltet haben,
/// aber höchstens einen davon aktuell auswählen. Freischalten ist idempotent und wählt den Titel
/// nicht automatisch aus. Entziehen, Sperren, Katalogmetadaten und Persistenz gehören nicht in
/// diese Foundation.
/// </remarks>
public sealed class CommunityTitles
{
    private readonly HashSet<TitleDefinitionId> _unlockedTitleDefinitionIds = [];

    private CommunityTitles(CommunityIdentityId communityIdentityId)
    {
        CommunityIdentityId = communityIdentityId;
    }

    /// <summary>
    /// Liefert die unveränderliche interne Community-Identität, der die Titel gehören.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert einen schreibgeschützten Snapshot der aktuell freigeschalteten Title-Definitionen.
    /// </summary>
    public IReadOnlyCollection<TitleDefinitionId> UnlockedTitleDefinitionIds =>
        Array.AsReadOnly(_unlockedTitleDefinitionIds.ToArray());

    /// <summary>
    /// Liefert die aktuell ausgewählte Title-Definition oder <see langword="null"/>, wenn kein Titel ausgewählt ist.
    /// </summary>
    public TitleDefinitionId? CurrentTitleDefinitionId { get; private set; }

    /// <summary>
    /// Erzeugt den initialen Title-Zustand einer Community-Identität ohne Freischaltungen und Auswahl.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <returns>Ein neuer leerer Title-Zustand.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="communityIdentityId"/> leer ist.</exception>
    public static CommunityTitles Create(CommunityIdentityId communityIdentityId)
    {
        EnsureValidCommunityIdentityId(communityIdentityId);
        return new CommunityTitles(communityIdentityId);
    }

    /// <summary>
    /// Schaltet eine Title-Definition idempotent frei.
    /// </summary>
    public bool Unlock(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);
        return _unlockedTitleDefinitionIds.Add(titleDefinitionId);
    }

    /// <summary>
    /// Wählt genau eine bereits freigeschaltete Title-Definition als aktuellen Titel aus.
    /// </summary>
    public void SelectCurrentTitle(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);

        if (!_unlockedTitleDefinitionIds.Contains(titleDefinitionId))
        {
            throw new TitleNotUnlockedException();
        }

        CurrentTitleDefinitionId = titleDefinitionId;
    }

    /// <summary>
    /// Entfernt die aktuelle Auswahl, ohne eine Freischaltung zu verändern.
    /// </summary>
    public void ClearCurrentTitle()
    {
        CurrentTitleDefinitionId = null;
    }

    private static void EnsureValidCommunityIdentityId(CommunityIdentityId communityIdentityId)
    {
        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Community-Title-Zustand benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }
    }

    private static void EnsureValidTitleDefinitionId(TitleDefinitionId titleDefinitionId)
    {
        if (titleDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Title-Definition-ID darf nicht leer sein.",
                nameof(titleDefinitionId));
        }
    }
}
