using System.Buffers;
using System.Text;

namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Validiert die für PostgreSQL-Textspalten verwendete kanonische Unicode-Semantik.
/// </summary>
internal static class AutomationText
{
    public static string Required(string? value, string parameterName, string fieldName, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} darf nicht leer oder aus Whitespace bestehen.", parameterName);
        }

        var normalized = value.Trim();
        EnsureValidUtf16AndLength(normalized, parameterName, fieldName, maximum);
        return normalized;
    }

    public static string? Optional(string? value, string parameterName, string fieldName, int maximum)
    {
        if (value is null)
        {
            return null;
        }

        return Required(value, parameterName, fieldName, maximum);
    }

    public static void EnsureCanonical(string? value, string parameterName, string fieldName, int maximum, bool allowNull)
    {
        if (value is null)
        {
            if (allowNull)
            {
                return;
            }

            throw new ArgumentException($"{fieldName} darf nicht null sein.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Der persistierte Wert für {fieldName} muss kanonisch getrimmt und nicht leer sein.", parameterName);
        }

        EnsureValidUtf16AndLength(value, parameterName, fieldName, maximum);
    }

    private static void EnsureValidUtf16AndLength(string value, string parameterName, string fieldName, int maximum)
    {
        if (value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException($"{fieldName} darf kein U+0000 enthalten.", parameterName);
        }

        var remaining = value.AsSpan();
        var scalarCount = 0;
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out _, out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                throw new ArgumentException($"{fieldName} muss gültiges, wohlgeformtes UTF-16 enthalten.", parameterName);
            }

            scalarCount++;
            remaining = remaining[charsConsumed..];
        }

        if (scalarCount > maximum)
        {
            throw new ArgumentException($"{fieldName} darf höchstens {maximum} Unicode-Skalarwerte enthalten.", parameterName);
        }
    }
}
