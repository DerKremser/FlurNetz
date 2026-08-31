namespace FlurNetz.Modules.Achievements.Domain;

/// <summary>
/// Beschreibt ein Achievement im implementation-eigenen Achievements-Katalog.
/// </summary>
/// <remarks>
/// Der erste Slice enthält ausschließlich die stabile Kennung, den kanonischen Anzeigenamen
/// und eine optionale kanonische Beschreibung. Fortschrittsregeln und Belohnungen gehören nicht
/// zu diesem Modell.
/// </remarks>
public sealed class AchievementDefinition
{
    /// <summary>
    /// Maximale Länge des kanonischen Anzeigenamens in .NET-<see cref="string.Length"/>-Zeichen.
    /// </summary>
    public const int MaxDisplayNameLength = 100;

    /// <summary>
    /// Maximale Länge der kanonischen Beschreibung in .NET-<see cref="string.Length"/>-Zeichen.
    /// </summary>
    public const int MaxDescriptionLength = 500;

    private AchievementDefinition(
        AchievementDefinitionId id,
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
    public AchievementDefinitionId Id { get; }

    /// <summary>
    /// Liefert den kanonisch getrimmten Anzeigenamen.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Liefert die kanonisch getrimmte Beschreibung oder <see langword="null"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Erstellt eine gültige Achievement-Definition.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung.</param>
    /// <param name="displayName">Der nicht leere Anzeigename.</param>
    /// <param name="description">Die optionale Beschreibung.</param>
    /// <returns>Eine neue, gültige Achievement-Definition.</returns>
    public static AchievementDefinition Create(
        AchievementDefinitionId id,
        string displayName,
        string? description)
    {
        return CreateValidated(id, displayName, description);
    }

    /// <summary>
    /// Rekonstruiert eine bereits persistierte Achievement-Definition.
    /// </summary>
    /// <remarks>
    /// Persistenzdaten werden mit exakt denselben Invarianten wie neue Definitionen geprüft;
    /// beschädigte Daten werden dadurch sichtbar abgelehnt und nicht still repariert.
    /// </remarks>
    /// <param name="id">Die persistierte Kennung.</param>
    /// <param name="displayName">Der persistierte Anzeigename.</param>
    /// <param name="description">Die persistierte Beschreibung.</param>
    /// <returns>Die rekonstruierte, gültige Definition.</returns>
    public static AchievementDefinition Rehydrate(
        AchievementDefinitionId id,
        string displayName,
        string? description)
    {
        EnsureValidId(id);
        EnsureCanonicalDisplayName(displayName);
        EnsureCanonicalDescription(description);

        return new AchievementDefinition(id, displayName, description);
    }

    /// <summary>
    /// Ändert den Anzeigenamen, sofern sich seine kanonische Form ändert.
    /// </summary>
    /// <param name="displayName">Der neue Anzeigename.</param>
    /// <returns><see langword="true"/>, wenn sich der Name geändert hat.</returns>
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
    /// <param name="description">Die neue Beschreibung oder <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, wenn sich die Beschreibung geändert hat.</returns>
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

    private static AchievementDefinition CreateValidated(
        AchievementDefinitionId id,
        string displayName,
        string? description)
    {
        EnsureValidId(id);

        return new AchievementDefinition(
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

    private static void EnsureCanonicalDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Der persistierte Anzeigename darf nicht leer oder aus Leerzeichen bestehen.",
                nameof(displayName));
        }

        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Der persistierte Anzeigename muss bereits kanonisch getrimmt sein.",
                nameof(displayName));
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            throw new ArgumentException(
                "Der Anzeigename darf höchstens " + MaxDisplayNameLength + " Zeichen lang sein.",
                nameof(displayName));
        }
    }

    private static void EnsureCanonicalDescription(string? description)
    {
        if (description is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Die persistierte Beschreibung darf nicht leer oder aus Leerzeichen bestehen.",
                nameof(description));
        }

        if (!string.Equals(description, description.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Die persistierte Beschreibung muss bereits kanonisch getrimmt sein.",
                nameof(description));
        }

        if (description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                "Die Beschreibung darf höchstens " + MaxDescriptionLength + " Zeichen lang sein.",
                nameof(description));
        }
    }

    private static void EnsureValidId(AchievementDefinitionId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Achievement-Definition benötigt eine nicht leere ID.",
                nameof(id));
        }
    }
}
