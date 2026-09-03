using System.Security.Claims;

namespace FlurNetz.Modules.Administration.Contracts.Security;

public interface IAdminAuthenticationService
{
    Task<AdminLoginResult> AuthenticateAsync(
        string? loginName,
        string? password,
        CancellationToken cancellationToken = default);

    Task<bool> ValidatePrincipalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
