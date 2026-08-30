namespace FlurNetz.Modules.Titles.Domain;

/// <summary>
/// Zeigt an, dass ein Titel ausgewählt werden soll, der für die Community-Identität nicht freigeschaltet ist.
/// </summary>
public sealed class TitleNotUnlockedException : Exception
{
    /// <summary>
    /// Erstellt die fachliche Ausnahme für die Auswahl eines nicht freigeschalteten Titels.
    /// </summary>
    public TitleNotUnlockedException()
        : base("Der Titel ist für diese Community-Identität nicht freigeschaltet.")
    {
    }
}
