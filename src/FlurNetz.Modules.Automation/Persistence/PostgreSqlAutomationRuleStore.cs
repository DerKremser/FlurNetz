using Dapper;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using System.Data.Common;

namespace FlurNetz.Modules.Automation.Persistence;

/// <summary>
/// Persistiert Automation-Rules ausschließlich in den Automation-eigenen Tabellen.
/// </summary>
public sealed class PostgreSqlAutomationRuleStore : IAutomationRuleStore
{
    private const string RuleColumns = """
        id AS Id, display_name AS DisplayName, description AS Description,
        trigger_type AS TriggerType, sort_order AS SortOrder,
        is_enabled AS IsEnabled, is_archived AS IsArchived,
        created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc
        """;

    private const string GetSql = $"""
        SELECT {RuleColumns}
        FROM automation_rules
        WHERE id = @Id;
        """;

    private const string ListSql = $"""
        SELECT {RuleColumns}
        FROM automation_rules
        ORDER BY sort_order ASC, id ASC;
        """;

    private const string RuntimeSql = $"""
        SELECT {RuleColumns}
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

    private const string InsertRuleSql = """
        INSERT INTO automation_rules
            (id, display_name, description, trigger_type, sort_order,
             is_enabled, is_archived, created_at_utc, updated_at_utc)
        VALUES
            (@Id, @DisplayName, @Description, @TriggerType, @SortOrder,
             @IsEnabled, @IsArchived, @CreatedAtUtc, @UpdatedAtUtc);
        """;

    private const string UpdateRuleSql = """
        UPDATE automation_rules
        SET display_name = @DisplayName,
            description = @Description,
            trigger_type = @TriggerType,
            sort_order = @SortOrder,
            is_enabled = @IsEnabled,
            is_archived = @IsArchived,
            updated_at_utc = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteConditionsSql = "DELETE FROM automation_rule_conditions WHERE automation_rule_id = @AutomationRuleId;";
    private const string DeleteActionsSql = "DELETE FROM automation_rule_actions WHERE automation_rule_id = @AutomationRuleId;";
    private const string InsertConditionSql = """
        INSERT INTO automation_rule_conditions
            (automation_rule_id, position, condition_type, community_identity_id,
             shop_offer_id, item_definition_id, amount)
        VALUES
            (@AutomationRuleId, @Position, @ConditionType, @CommunityIdentityId,
             @ShopOfferId, @ItemDefinitionId, @Amount);
        """;
    private const string InsertActionSql = """
        INSERT INTO automation_rule_actions
            (automation_rule_id, position, action_type, amount,
             notification_title, notification_message)
        VALUES
            (@AutomationRuleId, @Position, @ActionType, @Amount,
             @Title, @Message);
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>Erstellt den Management-Store.</summary>
    public PostgreSqlAutomationRuleStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistInsertAsync(rule, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AutomationRule?> GetAsync(AutomationRuleId ruleId, CancellationToken cancellationToken = default)
    {
        var id = AutomationRuleId.Create(ruleId.Value);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<RuleRow>(new CommandDefinition(GetSql, new { Id = id.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : await RehydrateAsync(row, connection, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AutomationRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RuleRow>(new CommandDefinition(ListSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var result = new List<AutomationRule>();
        foreach (var row in rows)
        {
            result.Add(await RehydrateAsync(row, connection, null, cancellationToken).ConfigureAwait(false));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    /// <inheritdoc />
    public async Task<AutomationRule?> MutateAsync(AutomationRuleId ruleId, Func<AutomationRule, bool> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var id = AutomationRuleId.Create(ruleId.Value);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var row = await transaction.Connection.QuerySingleOrDefaultAsync<RuleRow>(
                new CommandDefinition(GetForUpdateSql, new { Id = id.Value }, transaction: transaction.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            var rule = await RehydrateAsync(row, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            if (mutation(rule))
            {
                await PersistUpdateAsync(rule, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return rule;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private const string GetForUpdateSql = $"""
        SELECT {RuleColumns}
        FROM automation_rules
        WHERE id = @Id
        FOR UPDATE;
        """;

    private static async Task<AutomationRule> RehydrateAsync(RuleRow row, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var conditions = await connection.QueryAsync<ConditionRow>(new CommandDefinition(ConditionsSql, new { AutomationRuleId = row.Id }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var actions = await connection.QueryAsync<ActionRow>(new CommandDefinition(ActionsSql, new { AutomationRuleId = row.Id }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return AutomationRule.Rehydrate(
            AutomationRuleId.Create(row.Id),
            row.DisplayName,
            row.Description,
            row.TriggerType,
            conditions.Select(condition => AutomationCondition.Rehydrate(condition.Position, condition.ConditionType, condition.CommunityIdentityId, condition.ShopOfferId, condition.ItemDefinitionId, condition.Amount)),
            actions.Select(action => AutomationAction.Rehydrate(action.Position, action.ActionType, action.Amount, action.Title, action.Message)),
            row.SortOrder,
            row.IsEnabled,
            row.IsArchived,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    private static async Task PersistInsertAsync(AutomationRule rule, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(InsertRuleSql, RuleParameters(rule), transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await InsertChildrenAsync(rule, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PersistUpdateAsync(AutomationRule rule, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var updated = await connection.ExecuteAsync(new CommandDefinition(UpdateRuleSql, RuleParameters(rule), transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (updated != 1)
        {
            throw new InvalidOperationException("Die Automation-Rule konnte nicht eindeutig aktualisiert werden.");
        }

        await connection.ExecuteAsync(new CommandDefinition(DeleteConditionsSql, new { AutomationRuleId = rule.AutomationRuleId.Value }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(DeleteActionsSql, new { AutomationRuleId = rule.AutomationRuleId.Value }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await InsertChildrenAsync(rule, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertChildrenAsync(AutomationRule rule, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var condition in rule.Conditions)
        {
            await connection.ExecuteAsync(new CommandDefinition(InsertConditionSql, new
            {
                AutomationRuleId = rule.AutomationRuleId.Value,
                condition.Position,
                condition.ConditionType,
                condition.CommunityIdentityId,
                condition.ShopOfferId,
                condition.ItemDefinitionId,
                condition.Amount
            }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        foreach (var action in rule.Actions)
        {
            await connection.ExecuteAsync(new CommandDefinition(InsertActionSql, new
            {
                AutomationRuleId = rule.AutomationRuleId.Value,
                action.Position,
                action.ActionType,
                action.Amount,
                Title = action.Title,
                Message = action.Message
            }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private static object RuleParameters(AutomationRule rule) => new
    {
        Id = rule.AutomationRuleId.Value,
        rule.DisplayName,
        rule.Description,
        rule.TriggerType,
        rule.SortOrder,
        rule.IsEnabled,
        rule.IsArchived,
        rule.CreatedAtUtc,
        rule.UpdatedAtUtc
    };

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
