using System.Data.Common;
using System.Text.Json;
using Dapper;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Persistence;

public sealed class AdminAuditStore : IAdminAuditStore
{
    private const string InsertSql = """
        INSERT INTO administration_audit_entries
            (id, actor_community_identity_id, actor_login_name_snapshot, action, target_type,
             target_id, target_display_snapshot, risk_level, reason, result, occurred_at_utc,
             correlation_id, request_id, failure_code, change_summary, metadata)
        VALUES
            (@Id, @ActorCommunityIdentityId, @ActorLoginNameSnapshot, @Action, @TargetType,
             @TargetId, @TargetDisplaySnapshot, @RiskLevel, @Reason, @Result, @OccurredAtUtc,
             @CorrelationId, @RequestId, @FailureCode, CAST(@ChangeSummary AS jsonb), CAST(@Metadata AS jsonb));
        """;
    private const string ListSql = """
        SELECT id AS Id, actor_community_identity_id AS ActorCommunityIdentityId,
               actor_login_name_snapshot AS ActorLoginNameSnapshot, action AS Action,
               target_type AS TargetType, target_id AS TargetId,
               target_display_snapshot AS TargetDisplaySnapshot, risk_level AS RiskLevel,
               reason AS Reason, result AS Result, occurred_at_utc AS OccurredAtUtc,
               correlation_id AS CorrelationId, request_id AS RequestId,
               failure_code AS FailureCode, change_summary AS ChangeSummaryJson,
               metadata AS MetadataJson
        FROM administration_audit_entries
        WHERE (@TargetIdentityId IS NULL
               OR actor_community_identity_id = @TargetIdentityId
               OR target_id = CAST(@TargetIdentityId AS text))
        ORDER BY occurred_at_utc DESC, id DESC
        LIMIT @Take;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    public AdminAuditStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AppendAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            await AppendAsync(entry, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task AppendAsync(AdminAuditEntry entry, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var safeSummary = Redact(entry.ChangeSummary);
        var safeMetadata = Redact(entry.Metadata);
        return connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                entry.Id,
                ActorCommunityIdentityId = entry.ActorCommunityIdentityId.Value,
                entry.ActorLoginNameSnapshot,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                entry.TargetDisplaySnapshot,
                RiskLevel = entry.RiskLevel.ToString(),
                entry.Reason,
                Result = entry.Result.ToString(),
                entry.OccurredAtUtc,
                entry.CorrelationId,
                entry.RequestId,
                entry.FailureCode,
                ChangeSummary = JsonSerializer.Serialize(safeSummary),
                Metadata = JsonSerializer.Serialize(safeMetadata)
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminAuditEntry>> ListAsync(int take = 50, Guid? targetIdentityId = null, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(
                ListSql,
                new { Take = take, TargetIdentityId = targetIdentityId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(row => row.ToDomain()).ToArray());
    }

    private static IReadOnlyDictionary<string, string?> Redact(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ToDictionary(
            pair => pair.Key,
            pair => IsSensitive(pair.Key) ? "[redacted]" : pair.Value,
            StringComparer.Ordinal);
    }

    private static bool IsSensitive(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("hash", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("token", StringComparison.OrdinalIgnoreCase);

    private sealed class AuditRow
    {
        public Guid Id { get; set; }
        public Guid ActorCommunityIdentityId { get; set; }
        public string ActorLoginNameSnapshot { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string? TargetDisplaySnapshot { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string Result { get; set; } = string.Empty;
        public DateTimeOffset OccurredAtUtc { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }
        public string? FailureCode { get; set; }
        public string ChangeSummaryJson { get; set; } = "{}";
        public string MetadataJson { get; set; } = "{}";

        public AdminAuditEntry ToDomain() => new(
            Id,
            CommunityIdentityId.Create(ActorCommunityIdentityId),
            ActorLoginNameSnapshot,
            Action,
            TargetType,
            TargetId,
            TargetDisplaySnapshot,
            Enum.Parse<AdminRiskLevel>(RiskLevel, ignoreCase: false),
            Reason,
            Enum.Parse<AdminAuditOutcome>(Result, ignoreCase: false),
            OccurredAtUtc,
            CorrelationId,
            RequestId,
            FailureCode,
            Deserialize(ChangeSummaryJson),
            Deserialize(MetadataJson));

        private static IReadOnlyDictionary<string, string?> Deserialize(string json) =>
            JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }
}
