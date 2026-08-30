namespace FlurNetz.Modules.Progression.Domain;

/// <summary>
/// Repräsentiert die angesammelten Experience Points einer Community-Progression.
/// </summary>
/// <remarks>
/// Experience Points sind immer nicht-negativ. Dadurch kann der fachliche Gesamtwert
/// nicht unter null fallen. Die technische Obergrenze bleibt <see cref="long.MaxValue"/>;
/// ein darüber hinausgehender Wert wird als Overflow sichtbar abgelehnt.
/// </remarks>
public readonly record struct ExperiencePoints
{
    private readonly long _value;

    private ExperiencePoints(long value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den numerischen Experience-Points-Wert.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Liefert den gültigen Anfangswert ohne Experience Points.
    /// </summary>
    public static ExperiencePoints Zero => new(0);

    /// <summary>
    /// Erstellt einen nicht-negativen Experience-Points-Wert.
    /// </summary>
    /// <param name="value">Der anzulegende Gesamtwert.</param>
    /// <returns>Ein gültiger unveränderlicher Experience-Points-Wert.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="value"/> negativ ist.</exception>
    public static ExperiencePoints Create(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Experience Points dürfen nicht negativ sein.");
        }

        return new ExperiencePoints(value);
    }

    /// <summary>
    /// Addiert einen nicht-negativen Betrag, ohne den Wert mutierbar zu machen.
    /// </summary>
    /// <param name="amount">Der zu addierende Betrag; 0 ist als unveränderter Wert erlaubt.</param>
    /// <returns>Ein neuer Experience-Points-Wert mit dem addierten Betrag.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> negativ ist.</exception>
    /// <exception cref="OverflowException">Wenn die Addition <see cref="long.MaxValue"/> überschreiten würde.</exception>
    public ExperiencePoints Add(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Experience Points dürfen nur durch nicht-negative Beträge erhöht werden.");
        }

        if (amount > long.MaxValue - _value)
        {
            throw new OverflowException(
                "Die Addition würde die technische Obergrenze für Experience Points überschreiten.");
        }

        return new ExperiencePoints(_value + amount);
    }
}
