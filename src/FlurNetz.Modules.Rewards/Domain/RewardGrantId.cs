namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Bezeichnet einen fachlichen Grant-Record einer Reward-Definition-Ausführung.
/// </summary>
/// <remarks>
/// Die Kennung beschreibt bereits die fachliche Ausführung, ohne in diesem Foundation-
/// Schritt Persistenz oder einen Ausführungsstatus vorwegzunehmen.
/// </remarks>
public readonly record struct RewardGrantId
{
    private readonly Guid _value;

    private RewardGrantId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert des Grant-Records.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Grant-Kennung aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die dem Grant-Record zugeordnete GUID.</param>
    /// <returns>Eine unveränderliche Grant-Kennung.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static RewardGrantId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Reward-Grant-ID darf nicht leer sein.",
                nameof(value));
        }

        return new RewardGrantId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Grant-Kennung.
    /// </summary>
    /// <returns>Eine neue unveränderliche Grant-Kennung.</returns>
    public static RewardGrantId New() => Create(Guid.NewGuid());
}
