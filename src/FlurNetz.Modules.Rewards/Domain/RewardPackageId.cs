namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Bezeichnet ein fachliches Reward-Package innerhalb des Rewards-Moduls.
/// </summary>
/// <remarks>
/// Package- und Definitionskennungen sind absichtlich getrennte Fachtypen. Ein Package
/// fasst Definitionen zusammen, ist aber selbst keine einzelne Definition.
/// </remarks>
public readonly record struct RewardPackageId
{
    private readonly Guid _value;

    private RewardPackageId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert des Reward-Packages.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Reward-Package-Kennung aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die dem Package zugeordnete GUID.</param>
    /// <returns>Eine unveränderliche Reward-Package-Kennung.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static RewardPackageId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Reward-Package-ID darf nicht leer sein.",
                nameof(value));
        }

        return new RewardPackageId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Reward-Package-Kennung.
    /// </summary>
    /// <returns>Eine neue unveränderliche Reward-Package-Kennung.</returns>
    public static RewardPackageId New() => Create(Guid.NewGuid());
}
