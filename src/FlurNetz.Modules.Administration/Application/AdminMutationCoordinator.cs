using System.Data.Common;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Application;

/// <summary>
/// Besitzt die atomare Grenze für Owner-Mutation, Operation und Administration-Audit.
/// Owner-Code wird ausschließlich als transaction-aware Capability hineingereicht.
/// </summary>
public sealed class AdminMutationCoordinator
{
    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IAdminOperationStore operationStore;
    private readonly IAdminAuditStore auditStore;

    public AdminMutationCoordinator(IPostgreSqlConnectionFactory connectionFactory, IAdminOperationStore operationStore, IAdminAuditStore auditStore)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public async Task<AdminMutationResult> ExecuteAsync(
        AdminMutationCommand command,
        Func<DbConnection, DbTransaction, CancellationToken, Task> ownerMutation,
        Func<AdminAuditEntry> auditFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ownerMutation);
        ArgumentNullException.ThrowIfNull(auditFactory);
        var reservation = new AdminOperationReservation(
            command.RequestId,
            command.ActorCommunityIdentityId,
            command.OperationType,
            command.TargetType,
            command.TargetId,
            command.RequestFingerprint,
            command.CorrelationId,
            AdminMutationStatus.Reserved,
            AdminOperationAuditStatus.Pending,
            command.CreatedAtUtc,
            null);

        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await operationStore.ReserveAsync(reservation, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            if (stored.MutationStatus == AdminMutationStatus.Succeeded)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new AdminMutationResult(true, stored.MutationStatus);
            }

            if (!stored.IsNew && stored.MutationStatus == AdminMutationStatus.Reserved)
            {
                throw new AdminOperationInProgressException(command.RequestId);
            }

            await ownerMutation(transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await auditStore.AppendAsync(auditFactory(), transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await operationStore.CompleteAsync(command.RequestId, AdminMutationStatus.Succeeded, AdminOperationAuditStatus.Succeeded, DateTimeOffset.UtcNow, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AdminMutationResult(false, AdminMutationStatus.Succeeded);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Führt eine nicht-idempotente, aber auditpflichtige Admin-Mutation atomar aus.</summary>
    public async Task ExecuteAuditedAsync(
        Func<DbConnection, DbTransaction, CancellationToken, Task> ownerMutation,
        Func<AdminAuditEntry> auditFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerMutation);
        ArgumentNullException.ThrowIfNull(auditFactory);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            await ownerMutation(transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await auditStore.AppendAsync(auditFactory(), transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Atomare Variante für Owner-Mutationen, die ein Ergebnis zurückgeben.</summary>
    public async Task<TResult> ExecuteAuditedAsync<TResult>(
        Func<DbConnection, DbTransaction, CancellationToken, Task<TResult>> ownerMutation,
        Func<AdminAuditEntry> auditFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerMutation);
        ArgumentNullException.ThrowIfNull(auditFactory);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await ownerMutation(transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await auditStore.AppendAsync(auditFactory(), transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
