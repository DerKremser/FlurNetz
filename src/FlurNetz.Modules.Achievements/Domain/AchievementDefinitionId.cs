namespace FlurNetz.Modules.Achievements.Domain;

/// <summary>
/// Bezeichnet eine fachliche Achievement-Definition innerhalb des Achievements-Moduls.
/// </summary>
public readonly record struct AchievementDefinitionId
{
    private readonly Guid value;

    private AchievementDefinitionId(Guid value)
    {
        this.value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der Achievement-Definition.
    /// </summary>
    public Guid Value => value;

    /// <summary>
    /// Erstellt eine Achievement-Definition-ID aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die GUID der Achievement-Definition.</param>
    /// <returns>Eine unveränderliche Achievement-Definition-ID.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static AchievementDefinitionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Achievement-Definition-ID darf nicht leer sein.",
                nameof(value));
        }

        return new AchievementDefinitionId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Achievement-Definition-ID.
    /// </summary>
    /// <returns>Eine neue unveränderliche Achievement-Definition-ID.</returns>
    public static AchievementDefinitionId New() => Create(Guid.NewGuid());
}
