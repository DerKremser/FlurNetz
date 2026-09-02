using System.Text;
using System.Text.Json;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Api.Cursors;

/// <summary>Kodiert einen opaque, channelgebundenen Overlay-SSE-Cursor.</summary>
internal static class OverlayAlertCursorCodec
{
    public static string Encode(OverlayAlertCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CursorPayload(cursor.ChannelId.Value, cursor.CreatedAtUtc, cursor.AlertId));
        return Convert.ToBase64String(payload).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static bool TryDecode(string value, OverlayChannelId expectedChannelId, out OverlayAlertCursor cursor, out string error)
    {
        cursor = null!;
        error = "Der Overlay-Cursor ist ungültig.";
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
            var payload = JsonSerializer.Deserialize<CursorPayload>(Convert.FromBase64String(padded));
            if (payload is null || payload.ChannelId != expectedChannelId.Value)
            {
                error = "Der Overlay-Cursor gehört nicht zum angefragten Channel.";
                return false;
            }

            cursor = OverlayAlertCursor.Create(expectedChannelId, payload.CreatedAtUtc, payload.AlertId);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private sealed record CursorPayload(Guid ChannelId, DateTimeOffset CreatedAtUtc, Guid AlertId);
}
