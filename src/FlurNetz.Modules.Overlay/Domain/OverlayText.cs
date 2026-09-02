using System.Buffers;
using System.Text;

namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Gemeinsame kanonische Textvalidierung des Overlay-Moduls.</summary>
internal static class OverlayText
{
    public static string Required(string? value, string parameterName, string fieldName, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} darf nicht leer oder aus Whitespace bestehen.", parameterName);
        }

        var normalized = value.Trim();
        EnsureValid(normalized, parameterName, fieldName, maximum);
        return normalized;
    }

    public static string? Optional(string? value, string parameterName, string fieldName, int maximum) =>
        value is null ? null : Required(value, parameterName, fieldName, maximum);

    public static void EnsureCanonical(string? value, string parameterName, string fieldName, int maximum, bool allowNull)
    {
        if (value is null)
        {
            if (allowNull) return;
            throw new ArgumentException($"{fieldName} darf nicht null sein.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Der persistierte Wert für {fieldName} muss kanonisch getrimmt und nicht leer sein.", parameterName);
        }

        EnsureValid(value, parameterName, fieldName, maximum);
    }

    private static void EnsureValid(string value, string parameterName, string fieldName, int maximum)
    {
        if (value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException($"{fieldName} darf kein U+0000 enthalten.", parameterName);
        }

        var remaining = value.AsSpan();
        var scalarCount = 0;
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out _, out var consumed);
            if (status != OperationStatus.Done)
            {
                throw new ArgumentException($"{fieldName} muss gültiges, wohlgeformtes UTF-16 enthalten.", parameterName);
            }

            scalarCount++;
            remaining = remaining[consumed..];
        }

        if (scalarCount > maximum)
        {
            throw new ArgumentException($"{fieldName} darf höchstens {maximum} Unicode-Skalarwerte enthalten.", parameterName);
        }
    }
}
