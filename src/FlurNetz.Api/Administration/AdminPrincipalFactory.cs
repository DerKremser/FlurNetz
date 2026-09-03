using System.Security.Claims;
using FlurNetz.Modules.Administration.Contracts.Security;

namespace FlurNetz.Api.Administration;

public static class AdminPrincipalFactory
{
    public static ClaimsPrincipal Create(AdminCredentialSnapshot credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        var identity = new ClaimsIdentity(AdminAuthenticationDefaults.Scheme);
        identity.AddClaim(new Claim(
            AdminAuthenticationDefaults.CommunityIdentityIdClaim,
            credential.CommunityIdentityId.Value.ToString("D")));
        identity.AddClaim(new Claim(AdminAuthenticationDefaults.EmailClaim, credential.Email));
        identity.AddClaim(new Claim(
            AdminAuthenticationDefaults.CredentialVersionClaim,
            credential.CredentialVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(ClaimTypes.Name, credential.Email));
        identity.AddClaim(new Claim(AdminAuthenticationDefaults.SchemeClaim, AdminAuthenticationDefaults.Scheme));
        return new ClaimsPrincipal(identity);
    }
}
