using System.Text;
using System.Text.Json;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Api.Cursors;

/// <summary>
/// Kodiert und validiert den API-eigenen opaken Cursor der Shop-Kaufhistorie.
/// </summary>
internal static class ShopPurchaseHistoryCursorCodec
{
    private const int CurrentVersion = 1;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Kodiert einen validierten Application-Cursor als versionierte Base64Url-Payload.
    /// </summary>
    public static string Encode(ShopPurchaseHistoryCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var payload = new Dictionary<string, object?>
        {
            ["version"] = CurrentVersion,
            ["communityIdentityId"] = cursor.CommunityIdentityId.Value,
            ["purchasedAtUtc"] = cursor.PurchasedAtUtc,
            ["shopPurchaseId"] = cursor.ShopPurchaseId.Value
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Dekodiert, prüft und bindet einen API-Cursor an die angefragte Identität.
    /// </summary>
    public static bool TryDecode(
        string encoded,
        CommunityIdentityId requestedIdentityId,
        out ShopPurchaseHistoryCursor? cursor,
        out string error)
    {
        cursor = null;
        error = "Der History-Cursor ist ungültig.";

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
            var communityIdentityId = Guid.Empty;
            var purchasedAtUtc = default(DateTimeOffset);
            var shopPurchaseId = Guid.Empty;
            var hasVersion = false;
            var hasCommunityIdentityId = false;
            var hasPurchasedAtUtc = false;
            var hasShopPurchaseId = false;

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
                            || !property.Value.TryGetGuid(out communityIdentityId))
                        {
                            return false;
                        }

                        hasCommunityIdentityId = true;
                        break;

                    case "purchasedAtUtc":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !property.Value.TryGetDateTimeOffset(out purchasedAtUtc))
                        {
                            return false;
                        }

                        hasPurchasedAtUtc = true;
                        break;

                    case "shopPurchaseId":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !property.Value.TryGetGuid(out shopPurchaseId))
                        {
                            return false;
                        }

                        hasShopPurchaseId = true;
                        break;

                    default:
                        return false;
                }
            }

            if (!hasVersion
                || !hasCommunityIdentityId
                || !hasPurchasedAtUtc
                || !hasShopPurchaseId
                || version != CurrentVersion
                || communityIdentityId == Guid.Empty
                || shopPurchaseId == Guid.Empty
                || purchasedAtUtc.Offset != TimeSpan.Zero
                || purchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0
                || communityIdentityId != requestedIdentityId.Value)
            {
                return false;
            }

            cursor = ShopPurchaseHistoryCursor.Create(
                CommunityIdentityId.Create(communityIdentityId),
                purchasedAtUtc,
                ShopPurchaseId.Create(shopPurchaseId));
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
