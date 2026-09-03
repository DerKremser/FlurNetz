using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Security;

public sealed record AdminBootstrapConfiguration(
    CommunityIdentityId CommunityIdentityId,
    string LoginName,
    string InitialPassword);

public interface IAdminBootstrapper
{
    Task<bool> BootstrapAsync(
        AdminBootstrapConfiguration configuration,
        CancellationToken cancellationToken = default);
}

public interface IAdminCredentialRecovery
{
    Task<bool> RecoverAsync(
        CommunityIdentityId communityIdentityId,
        string newPassword,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
