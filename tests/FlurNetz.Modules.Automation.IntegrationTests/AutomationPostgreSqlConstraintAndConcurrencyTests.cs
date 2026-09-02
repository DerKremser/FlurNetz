using System.Data.Common;
using Dapper;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Automation.Migrations;
using FlurNetz.Modules.Automation.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using FlurNetz.Persistence.Transactions;
using Npgsql;

namespace FlurNetz.Modules.Automation.IntegrationTests;

/// <summary>Beweist die PostgreSQL-Constraints und die Runtime-/Management-Sperrgrenzen.</summary>
public sealed class AutomationPostgreSqlConstraintAndConcurrencyTests(AutomationPostgreSqlFixture database)
    : IClassFixture<AutomationPostgreSqlFixture>
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero).AddTicks(1230);

    private const string RuleInsertSql = """
        INSERT INTO automation_rules
            (id, display_name, description, trigger_type, sort_order,
             is_enabled, is_archived, created_at_utc, updated_at_utc)
        VALUES
            (@Id, @DisplayName, @Description, @TriggerType, @SortOrder,
             @IsEnabled, @IsArchived, @CreatedAtUtc, @UpdatedAtUtc);
        """;

    private const string ConditionInsertSql = """
        INSERT INTO automation_rule_conditions
            (automation_rule_id, position, condition_type, community_identity_id,
             shop_offer_id, item_definition_id, amount)
        VALUES
            (@AutomationRuleId, @Position, @ConditionType, @CommunityIdentityId,
             @ShopOfferId, @ItemDefinitionId, @Amount);
        """;

    private const string ActionInsertSql = """
        INSERT INTO automation_rule_actions
            (automation_rule_id, position, action_type, amount,
             notification_title, notification_message)
        VALUES
            (@AutomationRuleId, @Position, @ActionType, @Amount,
             @NotificationTitle, @NotificationMessage);
        """;

    private const string ExecutionInsertSql = """
        INSERT INTO automation_executions
            (id, automation_rule_id, trigger_message_id, trigger_message_type,
             trigger_schema_version, community_identity_id, trigger_occurred_at_utc,
             executed_at_utc)
        VALUES
            (@Id, @AutomationRuleId, @TriggerMessageId, @TriggerMessageType,
             @TriggerSchemaVersion, @CommunityIdentityId, @TriggerOccurredAtUtc,
             @ExecutedAtUtc);
        """;

    [Fact]
    public async Task DatabaseRejectsInvalidDirectWritesForAllAutomationConstraints()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        await using var connection = await factory.OpenConnectionAsync(TestToken);

        await AssertRejectsAsync(connection, RuleInsertSql, RuleParameters(
            Guid.NewGuid(), "Unknown trigger", "unknown-trigger", 0, false, false));
        await AssertRejectsAsync(connection, RuleInsertSql, RuleParameters(
            Guid.NewGuid(), "Negative sort", AutomationTriggerTypes.EngagementMessageRecorded, -1, false, false));
        await AssertRejectsAsync(connection, RuleInsertSql, RuleParameters(
            Guid.NewGuid(), "Archived enabled", AutomationTriggerTypes.EngagementMessageRecorded, 0, true, true));

        var conditionRuleId = Guid.NewGuid();
        await InsertRuleAsync(connection, conditionRuleId);
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 16,
            ConditionType = AutomationConditionTypes.CommunityIdentityEquals,
            CommunityIdentityId = Guid.NewGuid(),
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = (long?)null
        });
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 0,
            ConditionType = "unknown-condition",
            CommunityIdentityId = (Guid?)null,
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = (long?)null
        });
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 0,
            ConditionType = AutomationConditionTypes.CommunityIdentityEquals,
            CommunityIdentityId = Guid.Empty,
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = (long?)null
        });
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 0,
            ConditionType = AutomationConditionTypes.ShopPricePaidAtLeast,
            CommunityIdentityId = (Guid?)null,
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = -1L
        });
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 0,
            ConditionType = AutomationConditionTypes.ShopOfferIdEquals,
            CommunityIdentityId = Guid.NewGuid(),
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = (long?)null
        });
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = conditionRuleId,
            Position = 0,
            ConditionType = AutomationConditionTypes.ShopOfferIdEquals,
            CommunityIdentityId = (Guid?)null,
            ShopOfferId = Guid.NewGuid(),
            ItemDefinitionId = Guid.NewGuid(),
            Amount = (long?)null
        });

        var duplicateConditionRuleId = Guid.NewGuid();
        await InsertRuleAsync(connection, duplicateConditionRuleId);
        await connection.ExecuteAsync(new CommandDefinition(
            ConditionInsertSql,
            new
            {
                AutomationRuleId = duplicateConditionRuleId,
                Position = 0,
                ConditionType = AutomationConditionTypes.ShopPricePaidAtLeast,
                CommunityIdentityId = (Guid?)null,
                ShopOfferId = (Guid?)null,
                ItemDefinitionId = (Guid?)null,
                Amount = 1L
            },
            cancellationToken: TestToken));
        await AssertRejectsAsync(connection, ConditionInsertSql, new
        {
            AutomationRuleId = duplicateConditionRuleId,
            Position = 1,
            ConditionType = AutomationConditionTypes.ShopPricePaidAtLeast,
            CommunityIdentityId = (Guid?)null,
            ShopOfferId = (Guid?)null,
            ItemDefinitionId = (Guid?)null,
            Amount = 2L
        });

        var actionRuleId = Guid.NewGuid();
        await InsertRuleAsync(connection, actionRuleId);
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 16,
            ActionType = AutomationActionTypes.EconomyCredit,
            Amount = 1L,
            NotificationTitle = (string?)null,
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = "unknown-action",
            Amount = (long?)null,
            NotificationTitle = (string?)null,
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.EconomyCredit,
            Amount = 0L,
            NotificationTitle = (string?)null,
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.EconomyCredit,
            Amount = 1L,
            NotificationTitle = "forbidden",
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.NotificationCreate,
            Amount = (long?)null,
            NotificationTitle = (string?)null,
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.NotificationCreate,
            Amount = 1L,
            NotificationTitle = "Title",
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.NotificationCreate,
            Amount = (long?)null,
            NotificationTitle = " Title ",
            NotificationMessage = (string?)null
        });
        await AssertRejectsAsync(connection, ActionInsertSql, new
        {
            AutomationRuleId = actionRuleId,
            Position = 0,
            ActionType = AutomationActionTypes.NotificationCreate,
            Amount = (long?)null,
            NotificationTitle = "Title",
            NotificationMessage = " Message "
        });

        var executionRuleId = Guid.NewGuid();
        await InsertRuleAsync(connection, executionRuleId);
        await AssertRejectsAsync(connection, ExecutionInsertSql, new
        {
            Id = Guid.Empty,
            AutomationRuleId = executionRuleId,
            TriggerMessageId = Guid.NewGuid(),
            TriggerMessageType = AutomationTriggerTypes.EngagementMessageRecorded,
            TriggerSchemaVersion = 1,
            CommunityIdentityId = Guid.NewGuid(),
            TriggerOccurredAtUtc = Now,
            ExecutedAtUtc = Now
        });
        await AssertRejectsAsync(connection, ExecutionInsertSql, new
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = executionRuleId,
            TriggerMessageId = Guid.NewGuid(),
            TriggerMessageType = AutomationTriggerTypes.EngagementMessageRecorded,
            TriggerSchemaVersion = 2,
            CommunityIdentityId = Guid.NewGuid(),
            TriggerOccurredAtUtc = Now,
            ExecutedAtUtc = Now
        });
        await AssertRejectsAsync(connection, ExecutionInsertSql, new
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = executionRuleId,
            TriggerMessageId = Guid.NewGuid(),
            TriggerMessageType = "unknown-trigger",
            TriggerSchemaVersion = 1,
            CommunityIdentityId = Guid.NewGuid(),
            TriggerOccurredAtUtc = Now,
            ExecutedAtUtc = Now
        });

        var duplicateExecutionId = Guid.NewGuid();
        var duplicateMessageId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            ExecutionInsertSql,
            new
            {
                Id = duplicateExecutionId,
                AutomationRuleId = executionRuleId,
                TriggerMessageId = duplicateMessageId,
                TriggerMessageType = AutomationTriggerTypes.EngagementMessageRecorded,
                TriggerSchemaVersion = 1,
                CommunityIdentityId = Guid.NewGuid(),
                TriggerOccurredAtUtc = Now,
                ExecutedAtUtc = Now
            },
            cancellationToken: TestToken));
        await AssertRejectsAsync(connection, ExecutionInsertSql, new
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = executionRuleId,
            TriggerMessageId = duplicateMessageId,
            TriggerMessageType = AutomationTriggerTypes.EngagementMessageRecorded,
            TriggerSchemaVersion = 1,
            CommunityIdentityId = Guid.NewGuid(),
            TriggerOccurredAtUtc = Now,
            ExecutedAtUtc = Now
        });
    }

    [Fact]
    public async Task RuntimeSharedLockBlocksDisableUntilRuntimeTransactionCommits()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), "Locking", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], 0, Now);
        await store.AddAsync(rule, TestToken);
        _ = await store.MutateAsync(rule.Id, current => current.Enable(Now.AddMinutes(1)), TestToken);

        var runtimeStore = new PostgreSqlAutomationRuntimeStore();
        await using var runtimeTransaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken);
        Assert.Single(await runtimeStore.LoadActiveRulesAsync(
            AutomationTriggerTypes.EngagementMessageRecorded,
            runtimeTransaction.Connection,
            runtimeTransaction.Transaction,
            TestToken));

        var disable = new DisableAutomationRule(store, new FixedClock(Now.AddMinutes(2)));
        var disableTask = disable.ExecuteAsync(rule.Id, TestToken);
        await WaitForBlockedManagementMutationAsync(factory);
        Assert.False(disableTask.IsCompleted);

        await runtimeTransaction.CommitAsync(TestToken);
        await disableTask;

        var final = await store.GetAsync(rule.Id, TestToken);
        Assert.NotNull(final);
        Assert.False(final!.IsEnabled);

        var replace = new ReplaceAutomationRule(store, new FixedClock(Now.AddMinutes(3)));
        await replace.ExecuteAsync(
            rule.Id,
            "After disable",
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Updated")],
            1,
            TestToken);
        final = await store.GetAsync(rule.Id, TestToken);
        Assert.Equal("After disable", final!.DisplayName);
        Assert.Equal(1, final.SortOrder);
    }

    [Fact]
    public async Task RuntimeSharedLockBlocksReplaceUntilReleaseThenReturnsEnabledConflict()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), "Locked replace", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], 0, Now);
        await store.AddAsync(rule, TestToken);
        _ = await store.MutateAsync(rule.Id, current => current.Enable(Now.AddMinutes(1)), TestToken);

        var runtimeStore = new PostgreSqlAutomationRuntimeStore();
        await using var runtimeTransaction = await PostgreSqlTransaction.BeginAsync(factory, TestToken);
        Assert.Single(await runtimeStore.LoadActiveRulesAsync(
            AutomationTriggerTypes.EngagementMessageRecorded,
            runtimeTransaction.Connection,
            runtimeTransaction.Transaction,
            TestToken));

        var replace = new ReplaceAutomationRule(store, new FixedClock(Now.AddMinutes(2)));
        var replaceTask = replace.ExecuteAsync(
            rule.Id,
            "Must not replace while enabled",
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Updated")],
            1,
            TestToken);
        await WaitForBlockedManagementMutationAsync(factory);
        Assert.False(replaceTask.IsCompleted);

        await runtimeTransaction.CommitAsync(TestToken);
        var conflict = await Assert.ThrowsAsync<AutomationRuleConflictException>(() => replaceTask);
        Assert.Equal(rule.Id, conflict.RuleId);

        var disable = new DisableAutomationRule(store, new FixedClock(Now.AddMinutes(3)));
        await disable.ExecuteAsync(rule.Id, TestToken);
        await replace.ExecuteAsync(
            rule.Id,
            "Replaced after disable",
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Updated")],
            1,
            TestToken);

        var final = await store.GetAsync(rule.Id, TestToken);
        Assert.Equal("Replaced after disable", final!.DisplayName);
        Assert.False(final.IsEnabled);
        Assert.False(final.IsArchived);
    }

    private async Task PrepareAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new AutomationMigrationSource()).RunAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DROP TABLE IF EXISTS automation_executions CASCADE;
            DROP TABLE IF EXISTS automation_rule_actions CASCADE;
            DROP TABLE IF EXISTS automation_rule_conditions CASCADE;
            DROP TABLE IF EXISTS automation_rules CASCADE;
            DELETE FROM flurnetz_persistence.migration_history WHERE owner = 'Automation' AND version IN (1, 2);
            """,
            cancellationToken: TestToken));
        await new MigrationRunner(factory, new AutomationMigrationSource()).RunAsync(TestToken);
    }

    private static async Task InsertRuleAsync(DbConnection connection, Guid id)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            RuleInsertSql,
            RuleParameters(id, "Raw rule", AutomationTriggerTypes.EngagementMessageRecorded, 0, false, false),
            cancellationToken: TestToken));
    }

    private static object RuleParameters(
        Guid id,
        string displayName,
        string triggerType,
        int sortOrder,
        bool isEnabled,
        bool isArchived) => new
        {
            Id = id,
            DisplayName = displayName,
            Description = (string?)null,
            TriggerType = triggerType,
            SortOrder = sortOrder,
            IsEnabled = isEnabled,
            IsArchived = isArchived,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };

    private static async Task AssertRejectsAsync(DbConnection connection, string sql, object parameters)
    {
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: TestToken)));
    }

    private static async Task WaitForBlockedManagementMutationAsync(PostgreSqlConnectionFactory factory)
    {
        await using var observer = await factory.OpenConnectionAsync(TestToken);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var blocked = await observer.QuerySingleAsync<bool>(new CommandDefinition(
                """
                SELECT EXISTS
                (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE pid <> pg_backend_pid()
                      AND state = 'active'
                      AND wait_event_type = 'Lock'
                      AND query ILIKE '%automation_rules%'
                      AND query ILIKE '%FOR UPDATE%'
                );
                """,
                cancellationToken: TestToken));
            if (blocked) return;
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestToken);
        }

        throw new InvalidOperationException("Die Management-Mutation wurde in PostgreSQL nicht als wartend beobachtet.");
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
