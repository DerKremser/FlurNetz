namespace FlurNetz.Modules.Inventory.Domain;

/// <summary>
/// Repräsentiert eine nicht-negative Anzahl gleichartiger Inventory-Einheiten.
/// </summary>
/// <remarks>
/// Die Foundation modelliert ausschließlich ganzzahlige Mengen eines Item-Typs. Stack-Limits,
/// einzigartige Item-Instanzen oder Zustände einzelner Gegenstände werden bewusst nicht
/// vorweggenommen. Als unveränderlicher Werttyp gibt jede Änderung einen neuen Wert zurück.
/// </remarks>
public readonly record struct InventoryQuantity
{
    private readonly long _value;

    private InventoryQuantity(long value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert die aktuelle Anzahl.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Liefert den gültigen Bestand ohne Einheiten.
    /// </summary>
    public static InventoryQuantity Zero => new(0);

    /// <summary>
    /// Erstellt eine nicht-negative Inventory-Menge.
    /// </summary>
    /// <param name="value">Die anzulegende Gesamtmenge.</param>
    /// <returns>Eine gültige unveränderliche Inventory-Menge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="value"/> negativ ist.</exception>
    public static InventoryQuantity Create(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Eine Inventory-Menge darf nicht negativ sein.");
        }

        return new InventoryQuantity(value);
    }

    /// <summary>
    /// Fügt eine positive Anzahl hinzu und liefert die neue Menge.
    /// </summary>
    /// <param name="amount">Die hinzuzufügende positive Anzahl.</param>
    /// <returns>Eine neue Inventory-Menge mit dem erhöhten Bestand.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="OverflowException">Wenn die Erhöhung <see cref="long.MaxValue"/> überschreiten würde.</exception>
    public InventoryQuantity Add(long amount)
    {
        EnsurePositiveAmount(amount);

        if (amount > long.MaxValue - _value)
        {
            throw new OverflowException(
                "Die Bestandserhöhung würde die technische Obergrenze für die Inventory-Menge überschreiten.");
        }

        return new InventoryQuantity(_value + amount);
    }

    /// <summary>
    /// Entfernt eine positive Anzahl und liefert die neue Menge.
    /// </summary>
    /// <param name="amount">Die zu entnehmende positive Anzahl.</param>
    /// <returns>Eine neue Inventory-Menge nach der Entnahme.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="InsufficientInventoryQuantityException">Wenn der Bestand für die Entnahme nicht ausreicht.</exception>
    public InventoryQuantity Remove(long amount)
    {
        EnsurePositiveAmount(amount);

        if (amount > _value)
        {
            throw new InsufficientInventoryQuantityException();
        }

        return new InventoryQuantity(_value - amount);
    }

    private static void EnsurePositiveAmount(long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Eine Bestandsänderung muss positiv sein.");
        }
    }
}
