using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Domain;

/// <summary>Lokales administratives Credential, nicht ein allgemeines Benutzerprofil.</summary>
public sealed class AdminCredential
{
    private AdminCredential(
        CommunityIdentityId communityIdentityId,
        string loginName,
        string passwordHash,
        long credentialVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset passwordChangedAtUtc)
    {
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        LoginName = AdminLoginName.Canonicalize(loginName);
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
    }

    public CommunityIdentityId CommunityIdentityId { get; }
    public string LoginName { get; }
    public string NormalizedLoginName => AdminLoginName.Normalize(LoginName);
    internal string PasswordHash { get; private set; }
    public long CredentialVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset PasswordChangedAtUtc { get; private set; }

    public static AdminCredential Create(
        CommunityIdentityId communityIdentityId,
        string loginName,
        string passwordHash,
        DateTimeOffset nowUtc) =>
        new(
            communityIdentityId,
            loginName,
            passwordHash,
            1,
            EnsureUtc(nowUtc),
            EnsureUtc(nowUtc));

    public static AdminCredential Rehydrate(
        CommunityIdentityId communityIdentityId,
        string loginName,
        string passwordHash,
        long credentialVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset passwordChangedAtUtc) =>
        new(
            communityIdentityId,
            loginName,
            passwordHash,
            credentialVersion,
            createdAtUtc,
            passwordChangedAtUtc);

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

    public AdminCredentialSnapshot ToSnapshot() =>
        new(CommunityIdentityId, LoginName, CredentialVersion, CreatedAtUtc, PasswordChangedAtUtc);

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Zeitpunkt muss in UTC vorliegen.", nameof(value));
        }

        return value;
    }
}

public static class AdminLoginName
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 64;

    public static string Canonicalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Der LoginName darf nicht leer sein.", nameof(value));
        }

        var canonical = value.Trim();
        if (canonical.Length is < MinimumLength or > MaximumLength)
        {
            throw new ArgumentException(
                $"Der LoginName muss zwischen {MinimumLength} und {MaximumLength} Zeichen lang sein.",
                nameof(value));
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
