namespace FlurNetz.Modules.Engagement.Domain;

/// <summary>
/// Bezeichnet eine Engagement-Aktivität innerhalb des Engagement-Moduls.
/// </summary>
/// <remarks>
/// Die Kennung bleibt vorerst auf die Implementierung beschränkt, weil noch kein anderer
/// Modulvertrag sie benötigt. Externe Plattformkennungen sind kein Bestandteil dieses
/// fachlichen Identifiers; sie werden vor der Zuordnung zu einer Aktivität aufgelöst.
/// </remarks>
public readonly record struct EngagementActivityId
{
    private readonly Guid _value;

    private EngagementActivityId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der Engagement-Aktivität.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Engagement-Aktivitätskennung aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die der Aktivität zugeordnete GUID.</param>
    /// <returns>Eine unveränderliche Engagement-Aktivitätskennung.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static EngagementActivityId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Engagement-Aktivitäts-ID darf nicht leer sein.",
                nameof(value));
        }

        return new EngagementActivityId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Engagement-Aktivitätskennung.
    /// </summary>
    /// <returns>Eine neue unveränderliche Engagement-Aktivitätskennung.</returns>
    public static EngagementActivityId New() => Create(Guid.NewGuid());
}
