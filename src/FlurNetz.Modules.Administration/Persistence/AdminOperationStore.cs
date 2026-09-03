using System.Data.Common;
using Dapper;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;

namespace FlurNetz.Modules.Administration.Persistence;

public sealed class AdminOperationStore : IAdminOperationStore
{
    private const string SelectSql = """
        SELECT request_id AS RequestId, actor_community_identity_id AS ActorCommunityIdentityId,
               operation_type AS OperationType, target_type AS TargetType, target_id AS TargetId,
               request_fingerprint AS RequestFingerprint, correlation_id AS CorrelationId,
               mutation_status AS MutationStatus, audit_status AS AuditStatus,
               created_at_utc AS CreatedAtUtc, completed_at_utc AS CompletedAtUtc
        FROM administration_operations
        WHERE request_id = @RequestId;
        """;
    private const string InsertSql = """
        INSERT INTO administration_operations
            (request_id, actor_community_identity_id, operation_type, target_type, target_id,
             request_fingerprint, correlation_id, mutation_status, audit_status, created_at_utc)
        VALUES
            (@RequestId, @ActorCommunityIdentityId, @OperationType, @TargetType, @TargetId,
             @RequestFingerprint, @CorrelationId, @MutationStatus, @AuditStatus, @CreatedAtUtc)
        ON CONFLICT (request_id) DO NOTHING;
        """;
    private const string CompleteSql = """
        UPDATE administration_operations
        SET mutation_status = @MutationStatus,
            audit_status = @AuditStatus,
            completed_at_utc = @CompletedAtUtc
        WHERE request_id = @RequestId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    public AdminOperationStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AdminOperationReservation?> FindAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Die RequestId darf nicht leer sein.", nameof(requestId));
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<OperationRow>(new CommandDefinition(
                SelectSql, new { RequestId = requestId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public async Task<AdminOperationReservation> ReserveAsync(AdminOperationReservation operation, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (operation.RequestId == Guid.Empty) throw new ArgumentException("Die RequestId darf nicht leer sein.", nameof(operation));

        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                operation.RequestId,
                ActorCommunityIdentityId = operation.ActorCommunityIdentityId.Value,
                operation.OperationType,
                operation.TargetType,
                operation.TargetId,
                operation.RequestFingerprint,
                operation.CorrelationId,
                MutationStatus = operation.MutationStatus.ToString(),
                AuditStatus = operation.AuditStatus.ToString(),
                operation.CreatedAtUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var row = await connection.QuerySingleAsync<OperationRow>(new CommandDefinition(
                SelectSql, new { RequestId = operation.RequestId }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var existing = row.ToDomain();
        if (!string.Equals(existing.RequestFingerprint, operation.RequestFingerprint, StringComparison.Ordinal))
        {
            throw new AdminOperationConflictException(operation.RequestId);
        }

        return existing with { IsNew = inserted == 1 };
    }

    public async Task CompleteAsync(Guid requestId, AdminMutationStatus mutationStatus, AdminOperationAuditStatus auditStatus, DateTimeOffset completedAtUtc, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var count = await connection.ExecuteAsync(new CommandDefinition(
                CompleteSql,
                new { RequestId = requestId, MutationStatus = mutationStatus.ToString(), AuditStatus = auditStatus.ToString(), CompletedAtUtc = completedAtUtc },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (count != 1) throw new KeyNotFoundException($"Die AdminOperation '{requestId}' wurde nicht gefunden.");
    }

    private sealed class OperationRow
    {
        public Guid RequestId { get; set; }
        public Guid ActorCommunityIdentityId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string RequestFingerprint { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string MutationStatus { get; set; } = string.Empty;
        public string AuditStatus { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }

        public AdminOperationReservation ToDomain() => new(
            RequestId,
            CommunityIdentityId.Create(ActorCommunityIdentityId),
            OperationType,
            TargetType,
            TargetId,
            RequestFingerprint,
            CorrelationId,
            Enum.Parse<AdminMutationStatus>(MutationStatus),
            Enum.Parse<AdminOperationAuditStatus>(AuditStatus),
            CreatedAtUtc,
            CompletedAtUtc);
    }
}
