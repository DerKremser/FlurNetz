using System.Text;
using System.Text.Json;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Api.Cursors;

/// <summary>
/// Kodiert einen opaken, identity- und filtergebundenen Inbox-Cursor.
/// </summary>
internal static class NotificationInboxCursorCodec
{
    private const int CurrentVersion = 1;
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string Encode(NotificationInboxCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var payload = new Dictionary<string, object?>
        {
            ["version"] = CurrentVersion,
            ["communityIdentityId"] = cursor.CommunityIdentityId.Value,
            ["unreadOnly"] = cursor.UnreadOnly,
            ["createdAtUtc"] = cursor.CreatedAtUtc,
            ["notificationId"] = cursor.NotificationId.Value
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(
        string encoded,
        CommunityIdentityId requestedIdentityId,
        bool requestedUnreadOnly,
        out NotificationInboxCursor? cursor)
    {
        cursor = null;
        if (!TryDecodeBase64Url(encoded, out var bytes))
        {
            return false;
        }

        try
        {
            _ = StrictUtf8.GetString(bytes);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var version = 0;
            var identityId = Guid.Empty;
            var unreadOnly = false;
            var createdAtUtc = default(DateTimeOffset);
            var notificationId = Guid.Empty;
            var hasVersion = false;
            var hasIdentityId = false;
            var hasUnreadOnly = false;
            var hasCreatedAtUtc = false;
            var hasNotificationId = false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    return false;
                }

                switch (property.Name)
                {
                    case "version":
                        if (!property.Value.TryGetInt32(out version))
                        {
                            return false;
                        }

                        hasVersion = true;
                        break;
                    case "communityIdentityId":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !property.Value.TryGetGuid(out identityId))
                        {
                            return false;
                        }

                        hasIdentityId = true;
                        break;
                    case "unreadOnly":
                        if (property.Value.ValueKind is not JsonValueKind.True
                            and not JsonValueKind.False)
                        {
                            return false;
                        }

                        unreadOnly = property.Value.GetBoolean();
                        hasUnreadOnly = true;
                        break;
                    case "createdAtUtc":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !property.Value.TryGetDateTimeOffset(out createdAtUtc))
                        {
                            return false;
                        }

                        hasCreatedAtUtc = true;
                        break;
                    case "notificationId":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !property.Value.TryGetGuid(out notificationId))
                        {
                            return false;
                        }

                        hasNotificationId = true;
                        break;
                    default:
                        return false;
                }
            }

            if (!hasVersion
                || !hasIdentityId
                || !hasUnreadOnly
                || !hasCreatedAtUtc
                || !hasNotificationId
                || version != CurrentVersion
                || identityId == Guid.Empty
                || notificationId == Guid.Empty
                || identityId != requestedIdentityId.Value
                || unreadOnly != requestedUnreadOnly
                || createdAtUtc.Offset != TimeSpan.Zero
                || createdAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            {
                return false;
            }

            cursor = new NotificationInboxCursor(
                CommunityIdentityId.Create(identityId),
                unreadOnly,
                createdAtUtc,
                NotificationId.Create(notificationId));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryDecodeBase64Url(string encoded, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(encoded) || encoded.Any(character =>
                !(character is >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-' or '_')))
        {
            return false;
        }

        if (encoded.Length % 4 == 1)
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
