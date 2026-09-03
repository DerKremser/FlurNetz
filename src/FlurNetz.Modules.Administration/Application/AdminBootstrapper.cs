using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Application;

public sealed class AdminBootstrapper : IAdminBootstrapper
{
    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly ICommunityIdentityExistence identityExistence;
    private readonly IAdminCredentialStore credentialStore;
    private readonly IAdminPasswordHasher passwordHasher;

    public AdminBootstrapper(
        IPostgreSqlConnectionFactory connectionFactory,
        ICommunityIdentityExistence identityExistence,
        IAdminCredentialStore credentialStore,
        IAdminPasswordHasher passwordHasher)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.identityExistence = identityExistence ?? throw new ArgumentNullException(nameof(identityExistence));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<bool> BootstrapAsync(AdminBootstrapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var identityId = CommunityIdentityId.Create(configuration.CommunityIdentityId.Value);
        var loginName = AdminLoginName.Canonicalize(configuration.LoginName);
        AdminPasswordPolicy.Validate(configuration.InitialPassword);

        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await identityExistence.ExistsAsync(identityId, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false))
            {
                throw new AdminBootstrapConflictException($"Die konfigurierte Community-Identity '{identityId.Value}' existiert nicht.");
            }

            var credential = await credentialStore.GetByIdentityAsync(identityId, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            var loginCredential = await credentialStore.GetByLoginNameAsync(AdminLoginName.Normalize(loginName), transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            var hasRole = await credentialStore.HasRoleAssignmentAsync(identityId, AdminRole.Administrator, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            if (credential is not null && loginCredential is not null && hasRole)
            {
                if (!ReferenceEquals(credential, loginCredential)
                    && credential.CommunityIdentityId != loginCredential.CommunityIdentityId)
                {
                    throw new AdminBootstrapConflictException("Der konfigurierte LoginName gehört bereits zu einer anderen Identity.");
                }

                if (!string.Equals(credential.NormalizedLoginName, AdminLoginName.Normalize(loginName), StringComparison.Ordinal))
                {
                    throw new AdminBootstrapConflictException("Das vorhandene Credential verwendet einen anderen LoginName.");
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (credential is not null || loginCredential is not null || hasRole)
            {
                throw new AdminBootstrapConflictException("Der Administration-Bootstrap befindet sich in einem inkonsistenten Teilzustand.");
            }

            var now = DateTimeOffset.UtcNow;
            var newCredential = AdminCredential.Create(identityId, loginName, passwordHasher.Hash(configuration.InitialPassword), now);
            await credentialStore.AddCredentialAsync(newCredential, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await credentialStore.AddRoleAssignmentAsync(identityId, AdminRole.Administrator, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
