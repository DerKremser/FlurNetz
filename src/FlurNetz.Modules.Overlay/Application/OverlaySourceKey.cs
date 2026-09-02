using System.Security.Cryptography;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Erzeugt und hasht technische Browser-Source-Credentials.</summary>
internal static class OverlaySourceKey
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string Hash(string sourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
