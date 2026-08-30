namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Bezeichnet eine fachliche Reward-Definition innerhalb des Rewards-Moduls.
/// </summary>
/// <remarks>
/// Die Kennung bleibt ein eigener Fachtyp, damit Definitionen nicht versehentlich mit
/// Package- oder Grant-Kennungen vermischt werden. Die Kennung gehört zur internen
/// Rewards-Domain und ist deshalb kein öffentlicher Modulvertrag.
/// </remarks>
public readonly record struct RewardDefinitionId
{
    private readonly Guid _value;

    private RewardDefinitionId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der Reward-Definition.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Reward-Definitionskennung aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die der Definition zugeordnete GUID.</param>
    /// <returns>Eine unveränderliche Reward-Definitionskennung.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static RewardDefinitionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Reward-Definitions-ID darf nicht leer sein.",
                nameof(value));
        }

        return new RewardDefinitionId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Reward-Definitionskennung.
    /// </summary>
    /// <returns>Eine neue unveränderliche Reward-Definitionskennung.</returns>
    public static RewardDefinitionId New() => Create(Guid.NewGuid());
}
