using Dapper;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Automation.Migrations;
using FlurNetz.Modules.Automation.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Automation.IntegrationTests;

/// <summary>Prüft Automation-Migration, Store, Locking, Reservation und History gegen PostgreSQL.</summary>
public sealed class AutomationPostgreSqlIntegrationTests(AutomationPostgreSqlFixture database)
    : IClassFixture<AutomationPostgreSqlFixture>
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task MigrationIsAppliedOnceAndOwnsExactlyFourTablesWithoutCrossModuleForeignKeys()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await ResetAsync(factory);

        var source = new AutomationMigrationSource();
        var runner = new MigrationRunner(factory, source);
        var first = await runner.RunAsync(TestToken);
        var second = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), first);
        Assert.Equal(new MigrationRunResult(0, 1), second);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tables = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name LIKE 'automation_%'
            ORDER BY table_name;
            """,
            cancellationToken: TestToken))).ToArray();
        var fkTargets = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT target.relname
            FROM pg_constraint constraint_row
            JOIN pg_class source ON source.oid = constraint_row.conrelid
            JOIN pg_class target ON target.oid = constraint_row.confrelid
            WHERE source.relname LIKE 'automation_%' AND constraint_row.contype = 'f';
            """,
            cancellationToken: TestToken))).ToArray();
        var history = await connection.QuerySingleAsync<MigrationHistory>(new CommandDefinition(
            $"""
            SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
            FROM {MigrationRunner.MigrationHistoryTableName}
            WHERE owner = 'Automation' AND version = 1;
            """,
            cancellationToken: TestToken));

        Assert.Equal(["automation_executions", "automation_rule_actions", "automation_rule_conditions", "automation_rules"], tables);
        Assert.All(fkTargets, target => Assert.Contains(target, tables));
        Assert.Equal("Automation", history.Owner);
        Assert.Equal(1, history.Version);
        Assert.Equal("CreateAutomationRulesAndExecutions", history.Name);
        Assert.Equal(MigrationChecksum.Compute(Assert.Single(source.GetMigrations()).Sql), history.Checksum);
    }

    [Fact]
    public async Task RuleStoreRehydratesAndMutatesWithLifecycleAndRuntimeReservationIdempotency()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = new PostgreSqlAutomationRuleStore(factory);
        var runtime = new PostgreSqlAutomationRuntimeStore();
        var history = new PostgreSqlAutomationExecutionHistoryStore(factory);
        var communityId = Guid.NewGuid();
        var rule = AutomationRule.Create(
            AutomationRuleId.New(),
            "  Kaufregel  ",
            "  Beschreibung  ",
            AutomationTriggerTypes.ShopPurchaseCompleted,
            [AutomationCondition.Create(0, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 10)],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 5)],
            3,
            Now);

        await store.AddAsync(rule, TestToken);
        var loaded = await store.GetAsync(rule.AutomationRuleId, TestToken);
        Assert.NotNull(loaded);
        Assert.Equal("Kaufregel", loaded!.DisplayName);
        Assert.False(loaded.IsEnabled);
        Assert.Equal(3, loaded.SortOrder);

        _ = await store.MutateAsync(rule.AutomationRuleId, value => value.Enable(Now.AddMinutes(1)), TestToken);
        await using var transaction = await FlurNetz.Persistence.Transactions.PostgreSqlTransaction.BeginAsync(factory, TestToken);
        var active = await runtime.LoadActiveRulesAsync(AutomationTriggerTypes.ShopPurchaseCompleted, transaction.Connection, transaction.Transaction, TestToken);
        Assert.Single(active);

        var snapshot = new AutomationTriggerSnapshot(
            Guid.NewGuid(),
            AutomationTriggerTypes.ShopPurchaseCompleted,
            1,
            Now,
            communityId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            20,
            Now);
        var execution = AutomationExecution.Create(AutomationExecutionId.New(), rule.AutomationRuleId, snapshot, Now.AddMinutes(2));
        Assert.True(await runtime.ReserveExecutionAsync(execution, transaction.Connection, transaction.Transaction, TestToken));
        Assert.False(await runtime.ReserveExecutionAsync(execution, transaction.Connection, transaction.Transaction, TestToken));
        await transaction.CommitAsync(TestToken);

        var page = await history.ListAsync(rule.AutomationRuleId, null, 10, TestToken);
        var persisted = Assert.Single(page);
        Assert.Equal(execution.Id, persisted.Id);
        Assert.Equal(snapshot.TriggerMessageId, persisted.TriggerMessageId);
    }

    [Fact]
    public async Task ExecutionHistoryUsesStableKeysetPaginationWithoutOverlap()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = new PostgreSqlAutomationRuleStore(factory);
        var history = new PostgreSqlAutomationExecutionHistoryStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), "History", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")], Now);
        await store.AddAsync(rule, TestToken);

        var runtime = new PostgreSqlAutomationRuntimeStore();
        var executions = Enumerable.Range(1, 3).Select(index =>
        {
            var occurredAt = Now.AddMinutes(index);
            var snapshot = new AutomationTriggerSnapshot(
                Guid.NewGuid(), AutomationTriggerTypes.EngagementMessageRecorded, 1, occurredAt,
                Guid.NewGuid());
            return AutomationExecution.Create(AutomationExecutionId.New(), rule.Id, snapshot, occurredAt);
        }).ToArray();

        await using (var transaction = await FlurNetz.Persistence.Transactions.PostgreSqlTransaction.BeginAsync(factory, TestToken))
        {
            foreach (var execution in executions)
            {
                Assert.True(await runtime.ReserveExecutionAsync(execution, transaction.Connection, transaction.Transaction, TestToken));
            }

            await transaction.CommitAsync(TestToken);
        }

        var firstPage = await history.ListAsync(rule.Id, null, 2, TestToken);
        var cursor = new AutomationExecutionCursor(rule.Id, firstPage[^1].ExecutedAtUtc, firstPage[^1].Id);
        var secondPage = await history.ListAsync(rule.Id, cursor, 2, TestToken);

        Assert.Equal(2, firstPage.Count);
        Assert.Single(secondPage);
        Assert.Equal(executions[2].Id, firstPage[0].Id);
        Assert.Equal(executions[0].Id, secondPage[0].Id);
        Assert.Empty(firstPage.Select(item => item.Id).Intersect(secondPage.Select(item => item.Id)));
    }

    [Fact]
    public async Task FailedTransactionDoesNotLeaveRuleOrExecutionRows()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = new PostgreSqlAutomationRuleStore(factory);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), "Rollback", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")], Now);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var transaction = await FlurNetz.Persistence.Transactions.PostgreSqlTransaction.BeginAsync(factory, TestToken);
            await transaction.Connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO automation_rules (id, display_name, trigger_type, sort_order, is_enabled, is_archived, created_at_utc, updated_at_utc) VALUES (@Id, @Name, @Trigger, 0, FALSE, FALSE, @Now, @Now); INSERT INTO missing_automation_table VALUES (1);",
                new { Id = rule.AutomationRuleId.Value, Name = rule.DisplayName, Trigger = rule.TriggerType, Now },
                transaction: transaction.Transaction,
                cancellationToken: TestToken));
            await transaction.CommitAsync(TestToken);
        });

        Assert.Null(await store.GetAsync(rule.AutomationRuleId, TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));

    private async Task PrepareAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetAsync(factory);
        await new MigrationRunner(factory, new AutomationMigrationSource()).RunAsync(TestToken);
    }

    private static async Task ResetAsync(PostgreSqlConnectionFactory factory)
    {
        // Der Runner legt die technische History auch in einer frisch erstellten
        // Testdatenbank an; danach wird nur die Automation-Migration zurückgesetzt.
        await new MigrationRunner(factory, new AutomationMigrationSource()).RunAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            DROP TABLE IF EXISTS automation_executions CASCADE;
            DROP TABLE IF EXISTS automation_rule_actions CASCADE;
            DROP TABLE IF EXISTS automation_rule_conditions CASCADE;
            DROP TABLE IF EXISTS automation_rules CASCADE;
            DELETE FROM {MigrationRunner.MigrationHistoryTableName} WHERE owner = 'Automation' AND version = 1;
            """,
            cancellationToken: TestToken));
    }

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;
        public long Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
    }
}
