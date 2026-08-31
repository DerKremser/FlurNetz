namespace FlurNetz.Modules.Shop.Domain;

/// <summary>
/// Repräsentiert den nicht-negativen Preis eines Shop-Angebots in kleinsten ganzzahligen Einheiten.
/// </summary>
public readonly record struct ShopPrice
{
    private readonly long _value;

    private ShopPrice(long value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den ganzzahligen Preisbetrag.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Liefert den gültigen Preis eines kostenlosen Angebots.
    /// </summary>
    public static ShopPrice Zero => new(0);

    /// <summary>
    /// Erstellt einen nicht-negativen Shop-Preis.
    /// </summary>
    /// <param name="value">Der Preisbetrag.</param>
    /// <returns>Ein gültiger, unveränderlicher Shop-Preis.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="value"/> negativ ist.</exception>
    public static ShopPrice Create(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Der Shop-Preis darf nicht negativ sein.");
        }

        return new ShopPrice(value);
    }
}
