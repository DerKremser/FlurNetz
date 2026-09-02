namespace FlurNetz.Modules.Integrations.Contracts;

/// <summary>
/// Opaque, provider-eigene Benutzerkennung einer externen Plattform.
/// </summary>
public readonly record struct ExternalUserId
{
    private readonly string? value;

    private ExternalUserId(string value) => this.value = value;

    /// <summary>Liefert den unveränderten externen Identifier.</summary>
    public string Value => value ?? string.Empty;

    /// <summary>
    /// Erstellt eine gültige externe Benutzerkennung, ohne ihr Format zu interpretieren.
    /// </summary>
    /// <param name="value">Die opaque Kennung des Providers.</param>
    /// <exception cref="ArgumentException">Wenn die Kennung leer oder strukturell ungültig ist.</exception>
    public static ExternalUserId Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length is 0 or > 256 || string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Die externe Benutzerkennung muss zwischen 1 und 256 Zeichen lang sein und darf nicht mit Leerraum beginnen oder enden.",
                nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Die externe Benutzerkennung darf keine Steuerzeichen enthalten.",
                nameof(value));
        }

        return new ExternalUserId(value);
    }

    /// <summary>Versucht, eine externe Benutzerkennung zu erstellen.</summary>
    public static bool TryCreate(string? value, out ExternalUserId userId)
    {
        try
        {
            userId = Create(value!);
            return true;
        }
        catch (ArgumentException)
        {
            userId = default;
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
