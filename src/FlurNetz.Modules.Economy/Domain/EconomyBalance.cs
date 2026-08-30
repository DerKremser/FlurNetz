namespace FlurNetz.Modules.Economy.Domain;

/// <summary>
/// Repräsentiert einen nicht-negativen Economy-Wert in kleinsten ganzzahligen Einheiten.
/// </summary>
/// <remarks>
/// Der Wert bleibt bewusst neutral und trägt noch keine öffentliche Währungsbezeichnung.
/// Eine einzelne EconomyBalance modelliert den aktuell einzigen Saldo; eine
/// Multi-Currency-Struktur wird erst bei einem konkreten fachlichen Bedarf eingeführt.
/// Als unveränderlicher Werttyp gibt jede Änderung einen neuen Wert zurück.
/// </remarks>
public readonly record struct EconomyBalance
{
    private readonly long _value;

    private EconomyBalance(long value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den numerischen Economy-Wert.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Liefert den gültigen Anfangswert ohne Economy-Saldo.
    /// </summary>
    public static EconomyBalance Zero => new(0);

    /// <summary>
    /// Erstellt einen nicht-negativen Economy-Wert.
    /// </summary>
    /// <param name="value">Der anzulegende Gesamtwert.</param>
    /// <returns>Ein gültiger unveränderlicher Economy-Wert.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="value"/> negativ ist.</exception>
    public static EconomyBalance Create(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Der Economy-Saldo darf nicht negativ sein.");
        }

        return new EconomyBalance(value);
    }

    /// <summary>
    /// Schreibt einen positiven Betrag gut und liefert den neuen Saldo.
    /// </summary>
    /// <param name="amount">Der gutzuschreibende positive Betrag.</param>
    /// <returns>Ein neuer Economy-Wert mit der Gutschrift.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="OverflowException">Wenn die Gutschrift <see cref="long.MaxValue"/> überschreiten würde.</exception>
    public EconomyBalance Credit(long amount)
    {
        EnsurePositiveAmount(amount);

        if (amount > long.MaxValue - _value)
        {
            throw new OverflowException(
                "Die Gutschrift würde die technische Obergrenze für den Economy-Saldo überschreiten.");
        }

        return new EconomyBalance(_value + amount);
    }

    /// <summary>
    /// Bucht einen positiven Betrag ab und liefert den neuen Saldo.
    /// </summary>
    /// <param name="amount">Der abzubuchende positive Betrag.</param>
    /// <returns>Ein neuer Economy-Wert nach der Abbuchung.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="InsufficientEconomyBalanceException">Wenn der aktuelle Saldo für die Abbuchung nicht ausreicht.</exception>
    public EconomyBalance Debit(long amount)
    {
        EnsurePositiveAmount(amount);

        if (amount > _value)
        {
            throw new InsufficientEconomyBalanceException();
        }

        return new EconomyBalance(_value - amount);
    }

    private static void EnsurePositiveAmount(long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Ein Economy-Betrag muss positiv sein.");
        }
    }
}
