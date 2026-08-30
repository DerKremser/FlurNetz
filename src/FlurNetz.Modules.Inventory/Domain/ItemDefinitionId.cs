namespace FlurNetz.Modules.Inventory.Domain;

/// <summary>
/// Identifiziert eine fachliche Item-Definition innerhalb des Inventory-Moduls.
/// </summary>
/// <remarks>
/// Die Kennung trennt den Typ eines inventarisierbaren Gegenstands von seinem Bestand bei einer
/// Community-Identität. Namen, Darstellung, Kategorie und weitere Katalogmetadaten gehören noch
/// nicht in diese Foundation.
/// </remarks>
public readonly record struct ItemDefinitionId
{
    private readonly Guid _value;

    private ItemDefinitionId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der Item-Definition.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Item-Definition-ID aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die fachliche GUID der Item-Definition.</param>
    /// <returns>Eine unveränderliche Item-Definition-ID.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static ItemDefinitionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Item-Definition-ID darf nicht leer sein.",
                nameof(value));
        }

        return new ItemDefinitionId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Item-Definition-ID.
    /// </summary>
    /// <returns>Eine neue unveränderliche Item-Definition-ID.</returns>
    public static ItemDefinitionId New() => Create(Guid.NewGuid());
}
