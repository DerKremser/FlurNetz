namespace FlurNetz.Modules.Titles.Domain;

/// <summary>
/// Beschreibt einen Titel im implementation-eigenen Titles-Katalog.
/// </summary>
/// <remarks>
/// Der Katalog hält in diesem Slice ausschließlich die stabile fachliche ID, den
/// normalisierten Anzeigenamen und eine optionale normalisierte Beschreibung. Weitere
/// Präsentations- oder Freischaltungsmetadaten gehören nicht zu diesem Modell.
/// </remarks>
public sealed class TitleDefinition
{
    public const int MaxDisplayNameLength = 100;

    public const int MaxDescriptionLength = 500;

    private TitleDefinition(
        TitleDefinitionId id,
        string displayName,
        string? description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// Liefert die stabile fachliche Kennung der Definition.
    /// </summary>
    public TitleDefinitionId Id { get; }

    /// <summary>
    /// Liefert den kanonisch getrimmten Anzeigenamen.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Liefert die kanonisch getrimmte Beschreibung oder <see langword="null"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Erstellt eine gültige Title-Definition.
    /// </summary>
    public static TitleDefinition Create(
        TitleDefinitionId id,
        string displayName,
        string? description)
    {
        return CreateValidated(id, displayName, description);
    }

    /// <summary>
    /// Rekonstruiert eine bereits persistierte Title-Definition.
    /// </summary>
    /// <remarks>
    /// Persistenzdaten werden bewusst mit denselben Invarianten wie neue Definitionen
    /// validiert. Ein beschädigter Datenbankzustand wird dadurch sichtbar abgelehnt.
    /// </remarks>
    public static TitleDefinition Rehydrate(
        TitleDefinitionId id,
        string displayName,
        string? description)
    {
        return CreateValidated(id, displayName, description);
    }

    /// <summary>
    /// Ändert den Anzeigenamen, sofern sich seine kanonische Form ändert.
    /// </summary>
    public bool Rename(string displayName)
    {
        var normalizedDisplayName = NormalizeDisplayName(displayName);
        if (string.Equals(DisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayName = normalizedDisplayName;
        return true;
    }

    /// <summary>
    /// Ändert oder entfernt die Beschreibung, sofern sich ihre kanonische Form ändert.
    /// </summary>
    public bool ChangeDescription(string? description)
    {
        var normalizedDescription = NormalizeDescription(description);
        if (string.Equals(Description, normalizedDescription, StringComparison.Ordinal))
        {
            return false;
        }

        Description = normalizedDescription;
        return true;
    }

    private static TitleDefinition CreateValidated(
        TitleDefinitionId id,
        string displayName,
        string? description)
    {
        EnsureValidId(id);

        return new TitleDefinition(
            id,
            NormalizeDisplayName(displayName),
            NormalizeDescription(description));
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Der Anzeigename darf nicht leer oder aus Leerzeichen bestehen.",
                nameof(displayName));
        }

        var normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length > MaxDisplayNameLength)
        {
            throw new ArgumentException(
                "Der Anzeigename darf höchstens " + MaxDisplayNameLength + " Zeichen lang sein.",
                nameof(displayName));
        }

        return normalizedDisplayName;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                "Die Beschreibung darf höchstens " + MaxDescriptionLength + " Zeichen lang sein.",
                nameof(description));
        }

        return normalizedDescription;
    }

    private static void EnsureValidId(TitleDefinitionId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Title-Definition benötigt eine nicht leere ID.",
                nameof(id));
        }
    }
}
