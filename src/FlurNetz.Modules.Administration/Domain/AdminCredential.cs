using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Domain;

/// <summary>Lokales administratives Credential, nicht ein allgemeines Benutzerprofil.</summary>
public sealed class AdminCredential
{
    private AdminCredential(
        CommunityIdentityId communityIdentityId,
        string email,
        string passwordHash,
        long credentialVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset passwordChangedAtUtc,
        string? preferredCulture)
    {
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        Email = AdminEmail.Canonicalize(email);
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Ein Passwort-Hash ist erforderlich.", nameof(passwordHash));
        }

        if (credentialVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(credentialVersion));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero || passwordChangedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Credential-Zeitpunkte müssen in UTC vorliegen.");
        }

        PasswordHash = passwordHash;
        CredentialVersion = credentialVersion;
        CreatedAtUtc = createdAtUtc;
        PasswordChangedAtUtc = passwordChangedAtUtc;
        PreferredCulture = AdminPreferredCulture.NormalizePersisted(preferredCulture);
    }

    public CommunityIdentityId CommunityIdentityId { get; }
    public string Email { get; }
    public string NormalizedEmail => AdminEmail.Normalize(Email);
    internal string PasswordHash { get; private set; }
    public long CredentialVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset PasswordChangedAtUtc { get; private set; }
    public string? PreferredCulture { get; private set; }

    public static AdminCredential Create(
        CommunityIdentityId communityIdentityId,
        string email,
        string passwordHash,
        DateTimeOffset nowUtc) =>
        new(
            communityIdentityId,
            email,
            passwordHash,
            1,
            EnsureUtc(nowUtc),
            EnsureUtc(nowUtc),
            null);

    public static AdminCredential Rehydrate(
        CommunityIdentityId communityIdentityId,
        string email,
        string passwordHash,
        long credentialVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset passwordChangedAtUtc,
        string? preferredCulture = null) =>
        new(
            communityIdentityId,
            email,
            passwordHash,
            credentialVersion,
            createdAtUtc,
            passwordChangedAtUtc,
            preferredCulture);

    public void ChangePassword(string passwordHash, DateTimeOffset changedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Ein Passwort-Hash ist erforderlich.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        CredentialVersion = checked(CredentialVersion + 1);
        PasswordChangedAtUtc = EnsureUtc(changedAtUtc);
    }

    public void SetPreferredCulture(string preferredCulture)
    {
        PreferredCulture = AdminPreferredCulture.Require(preferredCulture);
    }

    public AdminCredentialSnapshot ToSnapshot() =>
        new(CommunityIdentityId, Email, CredentialVersion, CreatedAtUtc, PasswordChangedAtUtc, PreferredCulture);

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Zeitpunkt muss in UTC vorliegen.", nameof(value));
        }

        return value;
    }
}

public static class AdminPreferredCulture
{
    public const string German = "de";
    public const string English = "en";
    public const string Default = German;

    public static string Require(string? value) =>
        TryNormalize(value, out var normalized)
            ? normalized
            : throw new ArgumentException("Die bevorzugte Sprache muss de oder en sein.", nameof(value));

    public static string Resolve(string? value) => NormalizePersisted(value) ?? Default;

    public static string? NormalizePersisted(string? value) =>
        TryNormalize(value, out var normalized) ? normalized : null;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized is German or English;
    }
}

public static class AdminEmail
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 320;

    public static string Canonicalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Die E-Mail-Adresse darf nicht leer sein.", nameof(value));
        }

        var canonical = value.Trim();
        if (canonical.Length is < MinimumLength or > MaximumLength)
        {
            throw new ArgumentException(
                $"Die E-Mail-Adresse muss zwischen {MinimumLength} und {MaximumLength} Zeichen lang sein.",
                nameof(value));
        }

        if (canonical.Any(char.IsWhiteSpace)
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(canonical))
        {
            throw new ArgumentException("Die E-Mail-Adresse ist ungültig.", nameof(value));
        }

        return canonical;
    }

    public static string Normalize(string? value)
    {
        return Canonicalize(value).ToUpperInvariant();
    }
}

public static class AdminPasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;

    public static void Validate(string? password)
    {
        if (password is null)
        {
            throw new ArgumentException("Das Passwort ist erforderlich.", nameof(password));
        }

        if (password.Length is < MinimumLength or > MaximumLength)
        {
            throw new ArgumentException(
                $"Das Passwort muss zwischen {MinimumLength} und {MaximumLength} Zeichen lang sein.",
                nameof(password));
        }
    }
}
