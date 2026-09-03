using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using Microsoft.AspNetCore.Identity;

namespace FlurNetz.Modules.Administration.Application;

public sealed class AdminPasswordChange
{
    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IAdminCredentialStore credentialStore;
    private readonly IAdminPasswordHasher passwordHasher;
    private readonly IAdminAuditStore auditStore;

    public AdminPasswordChange(IPostgreSqlConnectionFactory connectionFactory, IAdminCredentialStore credentialStore, IAdminPasswordHasher passwordHasher, IAdminAuditStore auditStore)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public async Task<AdminCredentialSnapshot> ChangeAsync(AdminExecutionContext context, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AdminPasswordPolicy.Validate(newPassword);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var credential = await credentialStore.GetByIdentityAsync(context.ActorCommunityIdentityId, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false)
                ?? throw new AdminCredentialNotFoundException(context.ActorCommunityIdentityId);
            if (passwordHasher.Verify(credential.PasswordHash, currentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
            {
                throw new InvalidCredentialException();
            }

            credential.ChangePassword(passwordHasher.Hash(newPassword), DateTimeOffset.UtcNow);
            await credentialStore.ChangePasswordAsync(credential, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            var audit = new AdminAuditEntry(
                Guid.NewGuid(),
                context.ActorCommunityIdentityId,
                context.ActorLoginName,
                AdminAuditActions.CredentialChanged,
                "CommunityIdentity",
                context.ActorCommunityIdentityId.Value.ToString("D"),
                null,
                AdminRiskLevel.High,
                null,
                AdminAuditOutcome.Succeeded,
                DateTimeOffset.UtcNow,
                context.CorrelationId,
                context.RequestId,
                null,
                new Dictionary<string, string?> { ["CredentialChanged"] = "true" },
                new Dictionary<string, string?>());
            await auditStore.AppendAsync(audit, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return credential.ToSnapshot();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}

public sealed class InvalidCredentialException() : InvalidOperationException("Die aktuellen Anmeldedaten sind ungültig.");
