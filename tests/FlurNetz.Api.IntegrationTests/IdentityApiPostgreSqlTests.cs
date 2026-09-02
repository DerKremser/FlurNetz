using System.Net;
using System.Net.Http.Json;
using FlurNetz.Api.Contracts;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft den vollständigen HTTP-zu-PostgreSQL-Weg des ersten API-Slices.
/// </summary>
public sealed class IdentityApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task PostCreatesIdentityAndPersistsReturnedId()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var response = await PostIdentityAsync(client);

        await using var connection = await OpenConnectionAsync();
        await using var countCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM community_identities;",
            connection);
        await using var command = new NpgsqlCommand(
            "SELECT id FROM community_identities;",
            connection);
        var rowCount = (long)(await countCommand.ExecuteScalarAsync(TestToken))!;
        var storedId = (Guid)(await command.ExecuteScalarAsync(TestToken))!;

        Assert.NotEqual(Guid.Empty, response.Body.Id);
        Assert.Equal(1L, rowCount);
        Assert.Equal(response.Body.Id, storedId);
    }

    [Fact]
    public async Task HostRunsIdentityMigrationBeforeServingRequests()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        await using var connection = await OpenConnectionAsync();
        await using var tableCommand = new NpgsqlCommand(
            "SELECT to_regclass('public.community_identities') IS NOT NULL;",
            connection);
        await using var historyCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM flurnetz_persistence.migration_history WHERE owner = 'Identity' AND version = 1;",
            connection);

        var tableExists = (bool)(await tableCommand.ExecuteScalarAsync(TestToken))!;
        var migrationCount = (long)(await historyCommand.ExecuteScalarAsync(TestToken))!;

        Assert.True(tableExists);
        Assert.Equal(1L, migrationCount);
    }

    [Fact]
    public async Task MultiplePostsCreateDistinctPersistedIdentities()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var first = await PostIdentityAsync(client);
        var second = await PostIdentityAsync(client);

        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM community_identities;",
            connection);
        var rowCount = (long)(await command.ExecuteScalarAsync(TestToken))!;

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(Guid.Empty, first.Body.Id);
        Assert.NotEqual(Guid.Empty, second.Body.Id);
        Assert.NotEqual(first.Body.Id, second.Body.Id);
        Assert.Equal(2L, rowCount);
    }

    private async Task<(HttpStatusCode StatusCode, CreateCommunityIdentityResponse Body)> PostIdentityAsync(
        HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/identities");
        using var response = await client.SendAsync(request, TestToken);
        var body = await response.Content.ReadFromJsonAsync<CreateCommunityIdentityResponse>(TestToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        return (response.StatusCode, body!);
    }

    private async Task ResetDatabaseAsync()
    {
        await database.ResetDatabaseAsync(TestToken);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestToken);
        return connection;
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
