using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Minimale serverseitige Credentialdaten für Authentifizierung und Sessionprüfung.</summary>
public sealed record AdminCredentialSnapshot(
    CommunityIdentityId CommunityIdentityId,
    string Email,
    long CredentialVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset PasswordChangedAtUtc);

/// <summary>Ergebnis eines Loginversuchs ohne unterscheidbare externe Fehlergründe.</summary>
public sealed record AdminLoginResult(bool Succeeded, AdminCredentialSnapshot? Credential)
{
    public static AdminLoginResult Failure { get; } = new(false, null);
}
