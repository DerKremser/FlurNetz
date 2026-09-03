using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Application;

public sealed class AdminCredentialRecovery : IAdminCredentialRecovery
{
    private const string OperationType = "Administration.CredentialRecovery";
    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IAdminCredentialStore credentialStore;
    private readonly IAdminOperationStore operationStore;
    private readonly IAdminPasswordHasher passwordHasher;
    private readonly IAdminAuditStore auditStore;

    public AdminCredentialRecovery(IPostgreSqlConnectionFactory connectionFactory, IAdminCredentialStore credentialStore, IAdminOperationStore operationStore, IAdminPasswordHasher passwordHasher, IAdminAuditStore auditStore)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public async Task<bool> RecoverAsync(CommunityIdentityId communityIdentityId, string newPassword, Guid requestId, CancellationToken cancellationToken = default)
    {
        var identityId = CommunityIdentityId.Create(communityIdentityId.Value);
        if (requestId == Guid.Empty) throw new ArgumentException("Die Recovery RequestId darf nicht leer sein.", nameof(requestId));
        AdminPasswordPolicy.Validate(newPassword);

        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var operation = new AdminOperationReservation(
                requestId,
                identityId,
                OperationType,
                "CommunityIdentity",
                identityId.Value.ToString("D"),
                AdminRequestFingerprint.Compute(("identity", identityId.Value), ("operation", OperationType)),
                requestId.ToString("D"),
                AdminMutationStatus.Reserved,
                AdminOperationAuditStatus.Pending,
                DateTimeOffset.UtcNow,
                null);
            var reservation = await operationStore.ReserveAsync(operation, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            if (!reservation.IsNew && reservation.MutationStatus == AdminMutationStatus.Succeeded)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
            if (!reservation.IsNew && reservation.MutationStatus == AdminMutationStatus.Reserved)
            {
                throw new AdminOperationInProgressException(requestId);
            }

            var credential = await credentialStore.GetByIdentityAsync(identityId, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false)
                ?? throw new AdminCredentialNotFoundException(identityId);
            credential.ChangePassword(passwordHasher.Hash(newPassword), DateTimeOffset.UtcNow);
            await credentialStore.ChangePasswordAsync(credential, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await auditStore.AppendAsync(new AdminAuditEntry(
                Guid.NewGuid(),
                identityId,
                identityId.Value.ToString("D"),
                AdminAuditActions.CredentialRecovered,
                "CommunityIdentity",
                identityId.Value.ToString("D"),
                null,
                AdminRiskLevel.High,
                null,
                AdminAuditOutcome.Succeeded,
                DateTimeOffset.UtcNow,
                requestId.ToString("D"),
                requestId,
                null,
                new Dictionary<string, string?> { ["CredentialRecovered"] = "true" },
                new Dictionary<string, string?>()), transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await operationStore.CompleteAsync(requestId, AdminMutationStatus.Succeeded, AdminOperationAuditStatus.Succeeded, DateTimeOffset.UtcNow, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
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
