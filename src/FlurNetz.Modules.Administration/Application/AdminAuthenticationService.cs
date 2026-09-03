using System.Security.Claims;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Identity;

namespace FlurNetz.Modules.Administration.Application;

public sealed class AdminAuthenticationService : IAdminAuthenticationService
{
    private readonly IAdminCredentialStore credentialStore;
    private readonly IAdminPasswordHasher passwordHasher;
    private readonly string dummyHash;

    public AdminAuthenticationService(IAdminCredentialStore credentialStore, IAdminPasswordHasher passwordHasher)
    {
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        dummyHash = passwordHasher.Hash("flurnetz-admin-dummy-password");
    }

    public async Task<AdminLoginResult> AuthenticateAsync(string? loginName, string? password, CancellationToken cancellationToken = default)
    {
        AdminCredential? credential = null;
        string? normalized = null;
        try
        {
            normalized = AdminLoginName.Normalize(loginName);
            credential = await credentialStore.GetByLoginNameAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // Der Dummy-Verify unten hält den ungültigen Loginpfad bewusst ähnlich teuer.
        }

        var verification = passwordHasher.Verify(
            credential?.PasswordHash ?? dummyHash,
            password ?? string.Empty);

        if (credential is null
            || normalized is null
            || verification == PasswordVerificationResult.Failed
            || !await credentialStore.HasRoleAssignmentAsync(
                credential.CommunityIdentityId,
                AdminRole.Administrator,
                cancellationToken).ConfigureAwait(false))
        {
            return AdminLoginResult.Failure;
        }

        return new AdminLoginResult(true, credential.ToSnapshot());
    }

    public async Task<bool> ValidatePrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true
            || principal.FindFirstValue(AdminAuthenticationDefaults.SchemeClaim) != AdminAuthenticationDefaults.Scheme)
        {
            return false;
        }

        if (!Guid.TryParse(principal.FindFirstValue(AdminAuthenticationDefaults.CommunityIdentityIdClaim), out var id)
            || !long.TryParse(principal.FindFirstValue(AdminAuthenticationDefaults.CredentialVersionClaim), out var version)
            || string.IsNullOrWhiteSpace(principal.FindFirstValue(AdminAuthenticationDefaults.LoginNameClaim)))
        {
            return false;
        }

        var credential = await credentialStore.GetByIdentityAsync(
                FlurNetz.Modules.Identity.Contracts.CommunityIdentityId.Create(id),
                cancellationToken)
            .ConfigureAwait(false);
        return credential is not null
            && credential.CredentialVersion == version
            && string.Equals(
                credential.LoginName,
                principal.FindFirstValue(AdminAuthenticationDefaults.LoginNameClaim),
                StringComparison.Ordinal)
            && await credentialStore.HasRoleAssignmentAsync(
                credential.CommunityIdentityId,
                AdminRole.Administrator,
                cancellationToken).ConfigureAwait(false);
    }
}
