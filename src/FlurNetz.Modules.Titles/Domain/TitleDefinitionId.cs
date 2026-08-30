namespace FlurNetz.Modules.Titles.Domain;

/// <summary>
/// Bezeichnet eine fachliche Title-Definition innerhalb des Titles-Moduls.
/// </summary>
/// <remarks>
/// Die Kennung beschreibt ausschließlich die stabile Identität eines Titels. Anzeigename,
/// Beschreibung, Farbe, Icon, Seltenheit oder andere Katalogmetadaten gehören nicht in diese
/// Foundation.
/// </remarks>
public readonly record struct TitleDefinitionId
{
    private readonly Guid _value;

    private TitleDefinitionId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der Title-Definition.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine Title-Definition-ID aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die GUID der Title-Definition.</param>
    /// <returns>Eine unveränderliche Title-Definition-ID.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static TitleDefinitionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Title-Definition-ID darf nicht leer sein.",
                nameof(value));
        }

        return new TitleDefinitionId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Title-Definition-ID.
    /// </summary>
    /// <returns>Eine neue unveränderliche Title-Definition-ID.</returns>
    public static TitleDefinitionId New() => Create(Guid.NewGuid());
}
