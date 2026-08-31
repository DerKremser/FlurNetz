namespace FlurNetz.Modules.Shop.Contracts;

/// <summary>
/// Bezeichnet einen dauerhaft gespeicherten, erfolgreichen Shop-Kauf.
/// </summary>
public readonly record struct ShopPurchaseId
{
    private readonly Guid _value;

    private ShopPurchaseId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert des Kaufs.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Kauf-ID aus einer nicht leeren GUID.
    /// </summary>
    public static ShopPurchaseId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Shop-Purchase-ID darf nicht leer sein.", nameof(value));
        }

        return new ShopPurchaseId(value);
    }

    /// <summary>
    /// Erzeugt eine neue serverseitige Kauf-ID.
    /// </summary>
    public static ShopPurchaseId New() => Create(Guid.NewGuid());
}
