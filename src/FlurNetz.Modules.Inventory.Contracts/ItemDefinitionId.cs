namespace FlurNetz.Modules.Inventory.Contracts;

/// <summary>
/// Identifiziert eine fachliche Item-Definition innerhalb von FlurNetz.
/// </summary>
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
