using System.Net;
using System.Net.Http.Json;
using FlurNetz.Api.Contracts;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>Prüft die vollständige Automation-V1-Managementgrenze des API-Hosts.</summary>
public sealed class AutomationManagementApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 9, 2, 20, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task CrudLifecycleOrderingAndServerGeneratedIdAreExposed()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);

        using var host = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await host.CreateAdminClientAsync(TestToken);
        var identityId = Guid.NewGuid();
        var request = CreateShopRuleRequest(identityId, sortOrder: 4);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/automation/rules",
            request,
            TestToken);
        var created = await createResponse.Content.ReadFromJsonAsync<AutomationRuleResponse>(TestToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal($"/api/admin/automation/rules/{created.Id:D}", createResponse.Headers.Location?.OriginalString);
        Assert.False(created.IsEnabled);
        Assert.False(created.IsArchived);
        Assert.Equal("Kaufregel", created.DisplayName);
        Assert.Equal("Beschreibung", created.Description);
        Assert.Equal(2, created.Conditions.Count);
        Assert.Equal(2, created.Actions.Count);

        var secondRequest = CreateShopRuleRequest(Guid.NewGuid(), sortOrder: 1);
        using var secondResponse = await client.PostAsJsonAsync(
            "/api/admin/automation/rules",
            secondRequest,
            TestToken);
        var second = await secondResponse.Content.ReadFromJsonAsync<AutomationRuleResponse>(TestToken);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(second);

        var route = $"/api/admin/automation/rules/{created.Id:D}";
        using var getResponse = await client.GetAsync(route, TestToken);
        var loaded = await getResponse.Content.ReadFromJsonAsync<AutomationRuleResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, loaded?.Id);
        Assert.Equal(created.Conditions, loaded?.Conditions);
        Assert.Equal(created.Actions, loaded?.Actions);

        using var listResponse = await client.GetAsync("/api/admin/automation/rules", TestToken);
        var list = await listResponse.Content.ReadFromJsonAsync<AutomationRuleListResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(new[] { second!.Id, created.Id }, list?.Items.Select(item => item.Id));

        using var replaceResponse = await client.PutAsJsonAsync(
            route,
            request with
            {
                DisplayName = "  Ersetzt  ",
                Description = null,
                SortOrder = 2,
                Actions =
                [
                    new AutomationActionRequest(
                        "notification.create",
                        null,
                        "  Neue Nachricht  ",
                        "  Text  ")
                ]
            },
            TestToken);
        Assert.Equal(HttpStatusCode.NoContent, replaceResponse.StatusCode);
        var replaced = await client.GetFromJsonAsync<AutomationRuleResponse>(route, TestToken);
        Assert.Equal("Ersetzt", replaced?.DisplayName);
        Assert.Null(replaced?.Description);
        Assert.Single(replaced!.Actions);
        Assert.False(replaced.IsEnabled);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/enable", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/enable", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(route, request, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/disable", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/disable", null, TestToken)).StatusCode);
        var archiveRequest = new AdminActionRequest(Guid.NewGuid(), "archive rule for lifecycle test");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{route}/archive", archiveRequest, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{route}/archive", archiveRequest, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{route}/enable", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(route, request, TestToken)).StatusCode);
    }

    [Fact]
    public async Task InvalidManagementInputAndMissingRulesMapToProblemDetails()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);

        using var host = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await host.CreateAdminClientAsync(TestToken);
        var valid = CreateEngagementRuleRequest(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/admin/automation/rules/not-a-guid", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/admin/automation/rules/{Guid.NewGuid():D}", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with { TriggerType = "unsupported.trigger" },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Conditions =
                    [
                        new AutomationConditionRequest("shop.offer-id.equals", Guid.NewGuid(), null, null, null)
                    ]
                },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Conditions =
                    [
                        new AutomationConditionRequest("community-identity.equals", Guid.NewGuid(), null, null, null),
                        new AutomationConditionRequest("community-identity.equals", Guid.NewGuid(), null, null, null)
                    ]
                },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Actions =
                    [
                        new AutomationActionRequest("economy.credit", 0, null, null)
                    ]
                },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Actions =
                    [
                        new AutomationActionRequest("economy.credit", 1, "Überflüssig", null)
                    ]
                },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with { Actions = [] },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Conditions = Enumerable.Range(0, 17)
                        .Select(_ => new AutomationConditionRequest(
                            "community-identity.equals", Guid.NewGuid(), null, null, null))
                        .ToArray()
                },
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/admin/automation/rules",
                valid with
                {
                    Actions = Enumerable.Range(0, 17)
                        .Select(_ => new AutomationActionRequest(
                            "economy.credit", 1, null, null))
                        .ToArray()
                },
                TestToken)).StatusCode);

        using var unknownProperty = new StringContent(
            """{"displayName":"Valid","description":null,"triggerType":"engagement.message-recorded","conditions":[],"actions":[{"type":"notification.create","amount":null,"title":"Title","message":null,"unknown":true}],"sortOrder":0}""",
            System.Text.Encoding.UTF8,
            "application/json");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/api/admin/automation/rules", unknownProperty, TestToken)).StatusCode);
    }

    [Fact]
    public async Task ExecutionHistoryUsesRuleBoundKeysetCursorAndApiStartupRunsAutomationMigration()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);

        using var host = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await host.CreateAdminClientAsync(TestToken);
        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/automation/rules",
            CreateEngagementRuleRequest(Guid.NewGuid()),
            TestToken);
        var rule = await createResponse.Content.ReadFromJsonAsync<AutomationRuleResponse>(TestToken);
        Assert.NotNull(rule);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(TestToken);
            for (var index = 0; index < 3; index++)
            {
                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO automation_executions
                        (id, automation_rule_id, trigger_message_id, trigger_message_type,
                         trigger_schema_version, community_identity_id,
                         trigger_occurred_at_utc, executed_at_utc)
                    VALUES
                        (@Id, @RuleId, @MessageId, 'engagement.message-recorded', 1, @IdentityId, @Occurred, @Executed);
                    """,
                    connection);
                command.Parameters.AddWithValue("Id", Guid.NewGuid());
                command.Parameters.AddWithValue("RuleId", rule!.Id);
                command.Parameters.AddWithValue("MessageId", Guid.NewGuid());
                command.Parameters.AddWithValue("IdentityId", Guid.NewGuid());
                command.Parameters.AddWithValue("Occurred", Now.AddMinutes(index));
                command.Parameters.AddWithValue("Executed", Now.AddMinutes(index));
                await command.ExecuteNonQueryAsync(TestToken);
            }
        }

        var route = $"/api/admin/automation/rules/{rule!.Id:D}/executions";
        using var firstResponse = await client.GetAsync($"{route}?pageSize=2", TestToken);
        var first = await firstResponse.Content.ReadFromJsonAsync<AutomationExecutionPageResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(2, first?.Items.Count);
        Assert.NotNull(first?.NextCursor);

        using var secondResponse = await client.GetAsync(
            $"{route}?pageSize=2&cursor={Uri.EscapeDataString(first!.NextCursor!)}",
            TestToken);
        var second = await secondResponse.Content.ReadFromJsonAsync<AutomationExecutionPageResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Single(second!.Items);
        Assert.Null(second.NextCursor);
        Assert.True(first.Items[0].ExecutedAtUtc > first.Items[1].ExecutedAtUtc);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"{route}?pageSize=101", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"{route}?cursor=manipulated", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/admin/automation/rules/{Guid.NewGuid():D}/executions", TestToken)).StatusCode);

        await using var verification = new NpgsqlConnection(database.ConnectionString);
        await verification.OpenAsync(TestToken);
        await using var migrationCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM flurnetz_persistence.migration_history WHERE owner = 'Automation' AND version = 1;",
            verification);
        Assert.Equal(1L, (long)(await migrationCommand.ExecuteScalarAsync(TestToken))!);
    }

    private static AutomationRuleRequest CreateShopRuleRequest(Guid identityId, int sortOrder) =>
        new(
            "  Kaufregel  ",
            "  Beschreibung  ",
            "shop.purchase-completed",
            [
                new AutomationConditionRequest("community-identity.equals", identityId, null, null, null),
                new AutomationConditionRequest("shop.price-paid.at-least", null, null, null, 10)
            ],
            [
                new AutomationActionRequest("economy.credit", 5, null, null),
                new AutomationActionRequest("notification.create", null, "  Titel  ", "  Nachricht  ")
            ],
            sortOrder);

    private static AutomationRuleRequest CreateEngagementRuleRequest(Guid identityId) =>
        new(
            "Engagementregel",
            null,
            "engagement.message-recorded",
            [new AutomationConditionRequest("community-identity.equals", identityId, null, null, null)],
            [new AutomationActionRequest("notification.create", null, "Hinweis", null)],
            0);

    private void SkipIfUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
