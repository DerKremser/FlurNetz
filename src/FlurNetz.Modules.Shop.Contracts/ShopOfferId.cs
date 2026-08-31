namespace FlurNetz.Modules.Shop.Contracts;

/// <summary>
/// Bezeichnet ein stabiles fachliches Angebot im Shop.
/// </summary>
public readonly record struct ShopOfferId
{
    private readonly Guid _value;

    private ShopOfferId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert des Shop-Angebots.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Shop-Angebots-ID aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die fachliche GUID des Shop-Angebots.</param>
    /// <returns>Eine unveränderliche Shop-Angebots-ID.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static ShopOfferId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Shop-Angebots-ID darf nicht leer sein.",
                nameof(value));
        }

        return new ShopOfferId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Shop-Angebots-ID.
    /// </summary>
    /// <returns>Eine neue unveränderliche Shop-Angebots-ID.</returns>
    public static ShopOfferId New() => Create(Guid.NewGuid());
}
