using Dapper;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Persistence.Connections;

namespace FlurNetz.Modules.Automation.Persistence;

/// <summary>Liest die Automation-Execution-History mit stabiler Keyset-Pagination.</summary>
public sealed class PostgreSqlAutomationExecutionHistoryStore : IAutomationExecutionHistoryStore
{
    private const string ListSql = """
        SELECT id AS Id, automation_rule_id AS AutomationRuleId,
               trigger_message_id AS TriggerMessageId,
               trigger_message_type AS TriggerMessageType,
               trigger_schema_version AS TriggerSchemaVersion,
               community_identity_id AS CommunityIdentityId,
               trigger_occurred_at_utc AS TriggerOccurredAtUtc,
               executed_at_utc AS ExecutedAtUtc
        FROM automation_executions
        WHERE automation_rule_id = @AutomationRuleId
          AND (
              @HasCursor = FALSE
              OR executed_at_utc < @ExecutedAtUtc
              OR (executed_at_utc = @ExecutedAtUtc AND id < @ExecutionId)
          )
        ORDER BY executed_at_utc DESC, id DESC
        LIMIT @Take;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>Erstellt den History-Store.</summary>
    public PostgreSqlAutomationExecutionHistoryStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AutomationExecution>> ListAsync(AutomationRuleId ruleId, AutomationExecutionCursor? cursor, int take, CancellationToken cancellationToken = default)
    {
        if (take < 1) throw new ArgumentOutOfRangeException(nameof(take));
        var validId = AutomationRuleId.Create(ruleId.Value);
        if (cursor is not null && cursor.AutomationRuleId != validId)
        {
            throw new ArgumentException("Der Execution-Cursor gehört zu einer anderen Rule.", nameof(cursor));
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExecutionRow>(new CommandDefinition(
            ListSql,
            new
            {
                AutomationRuleId = validId.Value,
                HasCursor = cursor is not null,
                ExecutedAtUtc = cursor?.ExecutedAtUtc,
                ExecutionId = cursor?.Id.Value,
                Take = take
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(row => AutomationExecution.Rehydrate(
            AutomationExecutionId.Create(row.Id),
            AutomationRuleId.Create(row.AutomationRuleId),
            row.TriggerMessageId,
            row.TriggerMessageType,
            row.TriggerSchemaVersion,
            row.CommunityIdentityId,
            row.TriggerOccurredAtUtc,
            row.ExecutedAtUtc)).ToArray());
    }

    private sealed class ExecutionRow
    {
        public Guid Id { get; set; }
        public Guid AutomationRuleId { get; set; }
        public Guid TriggerMessageId { get; set; }
        public string TriggerMessageType { get; set; } = string.Empty;
        public int TriggerSchemaVersion { get; set; }
        public Guid CommunityIdentityId { get; set; }
        public DateTimeOffset TriggerOccurredAtUtc { get; set; }
        public DateTimeOffset ExecutedAtUtc { get; set; }
    }
}
