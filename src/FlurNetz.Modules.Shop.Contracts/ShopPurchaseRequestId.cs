namespace FlurNetz.Modules.Shop.Contracts;

/// <summary>
/// Bezeichnet einen global eindeutigen Kauf-Request zur Idempotenzsicherung.
/// </summary>
public readonly record struct ShopPurchaseRequestId
{
    private readonly Guid _value;

    private ShopPurchaseRequestId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert des Requests.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Request-ID aus einer nicht leeren GUID.
    /// </summary>
    public static ShopPurchaseRequestId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Shop-Purchase-Request-ID darf nicht leer sein.", nameof(value));
        }

        return new ShopPurchaseRequestId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Request-ID, beispielsweise für aufrufende Adapter oder Tests.
    /// </summary>
    public static ShopPurchaseRequestId New() => Create(Guid.NewGuid());
}
