using Dapper;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using System.Data.Common;

namespace FlurNetz.Modules.Automation.Persistence;

/// <summary>Führt ausschließlich transaction-aware Automation-Runtime-Queries aus.</summary>
public sealed class PostgreSqlAutomationRuntimeStore : IAutomationRuntimeStore
{
    private const string ActiveRulesSql = """
        SELECT id AS Id, display_name AS DisplayName, description AS Description,
               trigger_type AS TriggerType, sort_order AS SortOrder,
               is_enabled AS IsEnabled, is_archived AS IsArchived,
               created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc
        FROM automation_rules
        WHERE trigger_type = @TriggerType
          AND is_enabled = TRUE
          AND is_archived = FALSE
        ORDER BY sort_order ASC, id ASC
        FOR SHARE;
        """;
    private const string ConditionsSql = """
        SELECT position AS Position, condition_type AS ConditionType,
               community_identity_id AS CommunityIdentityId,
               shop_offer_id AS ShopOfferId, item_definition_id AS ItemDefinitionId,
               amount AS Amount
        FROM automation_rule_conditions
        WHERE automation_rule_id = @AutomationRuleId
        ORDER BY position ASC;
        """;
    private const string ActionsSql = """
        SELECT position AS Position, action_type AS ActionType,
               amount AS Amount, notification_title AS Title,
               notification_message AS Message
        FROM automation_rule_actions
        WHERE automation_rule_id = @AutomationRuleId
        ORDER BY position ASC;
        """;
    private const string ReserveSql = """
        INSERT INTO automation_executions
            (id, automation_rule_id, trigger_message_id, trigger_message_type,
             trigger_schema_version, community_identity_id, trigger_occurred_at_utc,
             executed_at_utc)
        VALUES
            (@Id, @AutomationRuleId, @TriggerMessageId, @TriggerMessageType,
             @TriggerSchemaVersion, @CommunityIdentityId, @TriggerOccurredAtUtc,
             @ExecutedAtUtc)
        ON CONFLICT (automation_rule_id, trigger_message_id) DO NOTHING
        RETURNING id;
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AutomationRule>> LoadActiveRulesAsync(string triggerType, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var rows = await connection.QueryAsync<RuleRow>(new CommandDefinition(ActiveRulesSql, new { TriggerType = triggerType }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var result = new List<AutomationRule>();
        foreach (var row in rows)
        {
            var conditions = await connection.QueryAsync<ConditionRow>(new CommandDefinition(ConditionsSql, new { AutomationRuleId = row.Id }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var actions = await connection.QueryAsync<ActionRow>(new CommandDefinition(ActionsSql, new { AutomationRuleId = row.Id }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            result.Add(AutomationRule.Rehydrate(
                AutomationRuleId.Create(row.Id),
                row.DisplayName,
                row.Description,
                row.TriggerType,
                conditions.Select(value => AutomationCondition.Rehydrate(value.Position, value.ConditionType, value.CommunityIdentityId, value.ShopOfferId, value.ItemDefinitionId, value.Amount)),
                actions.Select(value => AutomationAction.Rehydrate(value.Position, value.ActionType, value.Amount, value.Title, value.Message)),
                row.SortOrder,
                row.IsEnabled,
                row.IsArchived,
                row.CreatedAtUtc,
                row.UpdatedAtUtc));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    /// <inheritdoc />
    public async Task<bool> ReserveExecutionAsync(AutomationExecution execution, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var insertedId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            ReserveSql,
            new
            {
                Id = execution.Id.Value,
                AutomationRuleId = execution.AutomationRuleId.Value,
                execution.TriggerMessageId,
                execution.TriggerMessageType,
                execution.TriggerSchemaVersion,
                execution.CommunityIdentityId,
                execution.TriggerOccurredAtUtc,
                execution.ExecutedAtUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return insertedId.HasValue;
    }

    private sealed class RuleRow
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TriggerType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsArchived { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class ConditionRow
    {
        public int Position { get; set; }
        public string ConditionType { get; set; } = string.Empty;
        public Guid? CommunityIdentityId { get; set; }
        public Guid? ShopOfferId { get; set; }
        public Guid? ItemDefinitionId { get; set; }
        public long? Amount { get; set; }
    }

    private sealed class ActionRow
    {
        public int Position { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public long? Amount { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
    }
}
