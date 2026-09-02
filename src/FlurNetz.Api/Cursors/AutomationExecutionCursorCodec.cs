using System.Text;
using System.Text.Json;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Api.Cursors;

/// <summary>Kodiert einen opaken, versionierten und an eine Rule gebundenen History-Cursor.</summary>
internal static class AutomationExecutionCursorCodec
{
    private const int CurrentVersion = 1;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Kodiert einen Cursor als Base64URL.</summary>
    public static string Encode(AutomationExecutionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["version"] = CurrentVersion,
            ["automationRuleId"] = cursor.AutomationRuleId.Value,
            ["executedAtUtc"] = cursor.ExecutedAtUtc,
            ["id"] = cursor.Id.Value
        });
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Dekodiert und validiert einen Cursor inklusive Rule-Bindung.</summary>
    public static bool TryDecode(string? encoded, AutomationRuleId requestedRuleId, out AutomationExecutionCursor? cursor, out string error)
    {
        cursor = null;
        error = "Der Execution-Cursor ist ungültig.";
        if (!TryDecodeBase64Url(encoded, out var bytes)) return false;

        try
        {
            _ = StrictUtf8.GetString(bytes);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var version = 0;
            var ruleId = Guid.Empty;
            var executionId = Guid.Empty;
            var executedAt = default(DateTimeOffset);
            var hasVersion = false;
            var hasRuleId = false;
            var hasExecutionId = false;
            var hasExecutedAt = false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return false;
                switch (property.Name)
                {
                    case "version":
                        if (!property.Value.TryGetInt32(out version)) return false;
                        hasVersion = true;
                        break;
                    case "automationRuleId":
                        if (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetGuid(out ruleId)) return false;
                        hasRuleId = true;
                        break;
                    case "executedAtUtc":
                        if (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetDateTimeOffset(out executedAt)) return false;
                        hasExecutedAt = true;
                        break;
                    case "id":
                        if (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetGuid(out executionId)) return false;
                        hasExecutionId = true;
                        break;
                    default:
                        return false;
                }
            }

            if (!hasVersion || !hasRuleId || !hasExecutedAt || !hasExecutionId
                || version != CurrentVersion
                || ruleId == Guid.Empty
                || executionId == Guid.Empty
                || ruleId != requestedRuleId.Value
                || executedAt.Offset != TimeSpan.Zero
                || executedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            {
                return false;
            }

            cursor = new AutomationExecutionCursor(
                AutomationRuleId.Create(ruleId),
                executedAt,
                AutomationExecutionId.Create(executionId));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryDecodeBase64Url(string? encoded, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(encoded)
            || encoded.Any(character => !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            || encoded.Length % 4 == 1)
        {
            return false;
        }

        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        try
        {
            bytes = Convert.FromBase64String(base64);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
