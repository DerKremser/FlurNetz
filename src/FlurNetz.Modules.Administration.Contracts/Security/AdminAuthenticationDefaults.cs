namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Konstanten der von der Administration getrennten Cookie-Authentifizierung.</summary>
public static class AdminAuthenticationDefaults
{
    public const string Scheme = "FlurNetz.Admin";
    public const string CookieName = "__Host-FlurNetz.Admin";
    public const string CommunityIdentityIdClaim = "flurnetz:admin:community_identity_id";
    public const string LoginNameClaim = "flurnetz:admin:login_name";
    public const string CredentialVersionClaim = "flurnetz:admin:credential_version";
    public const string SchemeClaim = "flurnetz:admin:scheme";
}
