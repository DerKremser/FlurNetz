namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Beschreibt eine spätere Gutschrift eines positiven Betrags auf einen Economy-Saldo.
/// </summary>
/// <remarks>
/// Diese Definition beschreibt ausschließlich die gewünschte Wirkung. Sie besitzt keinen
/// Economy-Zustand und ruft Economy in diesem Foundation-Schritt nicht auf. Der Betrag
/// bleibt bewusst ein neutrales <see cref="long"/> ohne öffentliche Währungsbezeichnung.
/// Ein möglicher Overflow beim Zielsaldo gehört zur späteren Economy-Ausführung und wird
/// hier nicht vorweggenommen.
/// </remarks>
public sealed class EconomyBalanceRewardDefinition : RewardDefinition
{
    private EconomyBalanceRewardDefinition(RewardDefinitionId id, long amount)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Der Betrag einer Economy-Balance-Reward-Definition muss positiv sein.");
        }

        Amount = amount;
    }

    /// <summary>
    /// Liefert den positiven Betrag der beschriebenen Economy-Balance-Gutschrift.
    /// </summary>
    public long Amount { get; }

    /// <summary>
    /// Erstellt eine gültige Beschreibung einer späteren Economy-Balance-Gutschrift.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung der Reward-Definition.</param>
    /// <param name="amount">Der zu beschreibende positive Betrag.</param>
    /// <returns>Eine unveränderliche Economy-Balance-Reward-Definition.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="id"/> leer ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    public static EconomyBalanceRewardDefinition Create(
        RewardDefinitionId id,
        long amount)
    {
        return new EconomyBalanceRewardDefinition(id, amount);
    }
}
