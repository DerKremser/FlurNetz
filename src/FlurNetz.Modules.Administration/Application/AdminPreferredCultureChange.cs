using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Application;

/// <summary>Ändert ausschließlich die persistierte Sprache des aktuell ausgeführten Admins.</summary>
public sealed class AdminPreferredCultureChange(
    IPostgreSqlConnectionFactory connectionFactory,
    IAdminCredentialStore credentialStore,
    IAdminAuditStore auditStore)
{
    public async Task<AdminCredentialSnapshot> ChangeAsync(
        AdminExecutionContext context,
        string? preferredCulture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var normalizedCulture = AdminPreferredCulture.Require(preferredCulture);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var credential = await credentialStore.GetByIdentityAsync(
                    context.ActorCommunityIdentityId,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AdminCredentialNotFoundException(context.ActorCommunityIdentityId);

            var previousCulture = AdminPreferredCulture.Resolve(credential.PreferredCulture);
            credential.SetPreferredCulture(normalizedCulture);
            await credentialStore.ChangePreferredCultureAsync(
                    credential,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            await auditStore.AppendAsync(
                    new AdminAuditEntry(
                        Guid.NewGuid(),
                        context.ActorCommunityIdentityId,
                        context.ActorCommunityIdentityId.Value.ToString("D"),
                        AdminAuditActions.PreferredCultureChanged,
                        "AdminCredential",
                        context.ActorCommunityIdentityId.Value.ToString("D"),
                        null,
                        AdminRiskLevel.Low,
                        null,
                        AdminAuditOutcome.Succeeded,
                        DateTimeOffset.UtcNow,
                        context.CorrelationId,
                        context.RequestId,
                        null,
                        new Dictionary<string, string?>
                        {
                            ["PreferredCulture"] = normalizedCulture
                        },
                        new Dictionary<string, string?>
                        {
                            ["PreviousPreferredCulture"] = previousCulture
                        }),
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

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
