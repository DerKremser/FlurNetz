using System.Buffers;
using System.Text;

namespace FlurNetz.Modules.Notifications.Domain;

/// <summary>
/// Lokale Unicode-Validierung für kanonische Notification-Texte.
/// </summary>
/// <remarks>
/// Die Regeln bleiben bewusst im Notifications-Modul. Das verhindert einen vorsorglichen
/// Shared-Domain-Baustein für eine kleine, derzeit nur hier benötigte Textinvariante.
/// </remarks>
internal static class NotificationText
{
    public static string Required(
        string? value,
        string parameterName,
        string fieldName,
        int maximumScalarCount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} darf nicht leer oder aus Whitespace bestehen.",
                parameterName);
        }

        var normalized = value.Trim();
        EnsureValidUtf16AndLength(normalized, parameterName, fieldName, maximumScalarCount);
        return normalized;
    }

    public static string? Optional(
        string? value,
        string parameterName,
        string fieldName,
        int maximumScalarCount)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} darf nicht leer oder aus Whitespace bestehen.",
                parameterName);
        }

        var normalized = value.Trim();
        EnsureValidUtf16AndLength(normalized, parameterName, fieldName, maximumScalarCount);
        return normalized;
    }

    public static void EnsureCanonical(
        string? value,
        string parameterName,
        string fieldName,
        int maximumScalarCount,
        bool allowNull)
    {
        if (value is null)
        {
            if (allowNull)
            {
                return;
            }

            throw new ArgumentException($"{fieldName} darf nicht null sein.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"Der persistierte Wert für {fieldName} darf nicht leer oder aus Whitespace bestehen.",
                parameterName);
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Der persistierte Wert für {fieldName} muss bereits kanonisch getrimmt sein.",
                parameterName);
        }

        EnsureValidUtf16AndLength(value, parameterName, fieldName, maximumScalarCount);
    }

    private static void EnsureValidUtf16AndLength(
        string value,
        string parameterName,
        string fieldName,
        int maximumScalarCount)
    {
        if (value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException(
                $"{fieldName} darf kein U+0000 enthalten.",
                parameterName);
        }

        var remaining = value.AsSpan();
        var scalarCount = 0;
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out _, out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                throw new ArgumentException(
                    $"{fieldName} muss gültiges, wohlgeformtes UTF-16 enthalten.",
                    parameterName);
            }

            scalarCount++;
            remaining = remaining[charsConsumed..];
        }

        if (scalarCount > maximumScalarCount)
        {
            throw new ArgumentException(
                $"{fieldName} darf höchstens {maximumScalarCount} Unicode-Skalarwerte enthalten.",
                parameterName);
        }
    }
}
