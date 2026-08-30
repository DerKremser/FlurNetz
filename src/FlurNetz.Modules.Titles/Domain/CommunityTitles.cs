using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Titles.Domain;

/// <summary>
/// Hält die freigeschalteten Titel und die optionale aktuelle Auswahl einer Community-Identität.
/// </summary>
/// <remarks>
/// Eine Community-Identität kann beliebig viele unterschiedliche Titel freigeschaltet haben,
/// aber höchstens einen davon aktuell auswählen. Freischalten ist idempotent und wählt den Titel
/// nicht automatisch aus. Sperren des aktuellen Titels entfernt zugleich die aktuelle Auswahl.
/// Katalogmetadaten und Persistenz gehören nicht in diese Foundation.
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
    /// <param name="titleDefinitionId">Die freizuschaltende Title-Definition.</param>
    /// <returns>
    /// <see langword="true"/>, wenn die Berechtigungsmenge erweitert wurde; andernfalls
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Wenn die ID leer oder ungültig ist.</exception>
    public bool Unlock(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);
        return _unlockedTitleDefinitionIds.Add(titleDefinitionId);
    }

    /// <summary>
    /// Entfernt eine Titelberechtigung idempotent.
    /// </summary>
    /// <param name="titleDefinitionId">Die zu sperrende Title-Definition.</param>
    /// <returns>
    /// <see langword="true"/>, wenn die Berechtigung entfernt wurde; andernfalls
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Wird der aktuelle Titel gesperrt, wird die aktuelle Auswahl gleichzeitig entfernt.
    /// </remarks>
    /// <exception cref="ArgumentException">Wenn die ID leer oder ungültig ist.</exception>
    public bool Lock(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);

        if (!_unlockedTitleDefinitionIds.Remove(titleDefinitionId))
        {
            return false;
        }

        if (CurrentTitleDefinitionId == titleDefinitionId)
        {
            CurrentTitleDefinitionId = null;
        }

        return true;
    }

    /// <summary>
    /// Prüft, ob eine Title-Definition für diese Community-Identität freigeschaltet ist.
    /// </summary>
    /// <param name="titleDefinitionId">Die zu prüfende Title-Definition.</param>
    /// <returns><see langword="true"/>, wenn die ID freigeschaltet ist.</returns>
    /// <exception cref="ArgumentException">Wenn die ID leer oder ungültig ist.</exception>
    public bool IsUnlocked(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);
        return _unlockedTitleDefinitionIds.Contains(titleDefinitionId);
    }

    /// <summary>
    /// Wählt genau eine bereits freigeschaltete Title-Definition als aktuellen Titel aus.
    /// </summary>
    /// <param name="titleDefinitionId">Die auszuwählende Title-Definition.</param>
    /// <returns>
    /// <see langword="true"/>, wenn sich die Auswahl geändert hat; andernfalls
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Wenn die ID leer oder ungültig ist.</exception>
    /// <exception cref="TitleNotUnlockedException">
    /// Wenn die Title-Definition nicht freigeschaltet ist.
    /// </exception>
    public bool SetCurrent(TitleDefinitionId titleDefinitionId)
    {
        EnsureValidTitleDefinitionId(titleDefinitionId);

        if (!_unlockedTitleDefinitionIds.Contains(titleDefinitionId))
        {
            throw new TitleNotUnlockedException();
        }

        if (CurrentTitleDefinitionId == titleDefinitionId)
        {
            return false;
        }

        CurrentTitleDefinitionId = titleDefinitionId;
        return true;
    }

    /// <summary>
    /// Entfernt die aktuelle Auswahl, ohne eine Freischaltung zu verändern.
    /// </summary>
    /// <returns>
    /// <see langword="true"/>, wenn eine Auswahl entfernt wurde; andernfalls
    /// <see langword="false"/>.
    /// </returns>
    public bool ClearCurrent()
    {
        if (CurrentTitleDefinitionId is null)
        {
            return false;
        }

        CurrentTitleDefinitionId = null;
        return true;
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
