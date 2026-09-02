namespace FlurNetz.Modules.Integrations.Contracts;

/// <summary>
/// Stabile, kanonische Kennung eines externen Integrationsproviders.
/// </summary>
/// <remarks>
/// Der Wert ist absichtlich kein Enum. Neue Provider können dadurch ergänzt werden, ohne
/// diesen Cross-Module-Contract zu ändern oder eine Plugin-Registry einzuführen.
/// </remarks>
public readonly record struct IntegrationProviderKey
{
    private readonly string? value;

    private IntegrationProviderKey(string value) => this.value = value;

    /// <summary>Liefert die kanonische String-Repräsentation.</summary>
    public string Value => value ?? string.Empty;

    /// <summary>Der für V1 vorgesehene Twitch-Provider.</summary>
    public static IntegrationProviderKey Twitch => Create("twitch");

    /// <summary>
    /// Erstellt einen kanonischen Provider-Key.
    /// </summary>
    /// <param name="value">Ein alphanumerischer Key mit optionalen Bindestrichen.</param>
    /// <exception cref="ArgumentException">Wenn der Key ungültig ist.</exception>
    public static IntegrationProviderKey Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var canonical = value.Trim().ToLowerInvariant();
        if (canonical.Length is 0 or > 64)
        {
            throw new ArgumentException(
                "Der Integrations-Provider-Key muss zwischen 1 und 64 Zeichen lang sein.",
                nameof(value));
        }

        for (var index = 0; index < canonical.Length; index++)
        {
            var character = canonical[index];
            var valid = character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-';

            if (!valid || (character == '-' && (index == 0 || index == canonical.Length - 1)))
            {
                throw new ArgumentException(
                    "Der Integrations-Provider-Key darf nur Kleinbuchstaben, Ziffern und Bindestriche enthalten.",
                    nameof(value));
            }
        }

        return new IntegrationProviderKey(canonical);
    }

    /// <summary>Versucht, einen Provider-Key zu erstellen.</summary>
    public static bool TryCreate(string? value, out IntegrationProviderKey key)
    {
        try
        {
            key = Create(value!);
            return true;
        }
        catch (ArgumentException)
        {
            key = default;
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
