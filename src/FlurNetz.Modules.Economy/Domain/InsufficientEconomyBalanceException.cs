namespace FlurNetz.Modules.Economy.Domain;

/// <summary>
/// Zeigt an, dass ein fachlich gültiger Abbuchungsversuch den vorhandenen Saldo übersteigen würde.
/// </summary>
public sealed class InsufficientEconomyBalanceException : Exception
{
    /// <summary>
    /// Erstellt die fachliche Ausnahme für einen unzureichenden Economy-Saldo.
    /// </summary>
    public InsufficientEconomyBalanceException()
        : base("Der Economy-Saldo reicht für die Abbuchung nicht aus.")
    {
    }
}
