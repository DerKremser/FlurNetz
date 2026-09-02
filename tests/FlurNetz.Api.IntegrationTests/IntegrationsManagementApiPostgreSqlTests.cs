using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlurNetz.Api.Contracts;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>Prüft die Integrations-Management-Grenze vollständig bis PostgreSQL.</summary>
public sealed class IntegrationsManagementApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    private const string MappingsRoute = "/api/admin/integrations/external-identities";

    [Fact]
    public async Task ApiStartupRunsIntegrationsMigrationAndLinkPersistsMapping()
    {
        SkipIfUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var identityId = await CreateIdentityAsync(client);
        var response = await LinkAsync(client, identityId, "twitch-user");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("twitch", response.Body!.Provider);
        Assert.Equal("twitch-user", response.Body.ExternalUserId);
        Assert.Equal(identityId, response.Body.CommunityIdentityId);

        await using var connection = await OpenConnectionAsync();
        await using var tableCommand = new NpgsqlCommand(
            "SELECT to_regclass('public.integration_external_identity_mappings') IS NOT NULL;",
            connection);
        await using var historyCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM flurnetz_persistence.migration_history WHERE owner = 'Integrations' AND version = 1;",
            connection);
        await using var countCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM integration_external_identity_mappings;",
            connection);

        Assert.True((bool)(await tableCommand.ExecuteScalarAsync(TestToken))!);
        Assert.Equal(1L, (long)(await historyCommand.ExecuteScalarAsync(TestToken))!);
        Assert.Equal(1L, (long)(await countCommand.ExecuteScalarAsync(TestToken))!);
    }

    [Fact]
    public async Task LinkIsIdempotentGetListAndUnlinkAreSupported()
    {
        SkipIfUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var identityId = await CreateIdentityAsync(client);
        var first = await LinkAsync(client, identityId, "same-user");
        var second = await LinkAsync(client, identityId, "same-user");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(first.Body, second.Body);

        using var getResponse = await client.GetAsync(
            $"{MappingsRoute}/twitch/same-user",
            TestToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<ExternalIdentityMappingResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(first.Body, getBody);

        using var listResponse = await client.GetAsync(
            $"{MappingsRoute}/community/{identityId:D}",
            TestToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ExternalIdentityMappingListResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listBody);
        Assert.Single(listBody!.Items);

        using var deleteResponse = await client.DeleteAsync(
            $"{MappingsRoute}/twitch/same-user",
            TestToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var afterDelete = await client.GetAsync(
            $"{MappingsRoute}/twitch/same-user",
            TestToken);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);

        await using var connection = await OpenConnectionAsync();
        await using var mappingCountCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM integration_external_identity_mappings;",
            connection);
        await using var identityCountCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM community_identities;",
            connection);
        Assert.Equal(0L, (long)(await mappingCountCommand.ExecuteScalarAsync(TestToken))!);
        Assert.Equal(1L, (long)(await identityCountCommand.ExecuteScalarAsync(TestToken))!);
    }

    [Fact]
    public async Task InvalidInputUnknownIdentityAndReassignmentReturnProblemDetails()
    {
        SkipIfUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var identityId = await CreateIdentityAsync(client);

        using var invalidProviderResponse = await client.PostAsJsonAsync(
            MappingsRoute,
            new ExternalIdentityMappingRequest("Twitch/provider", "123", identityId),
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidProviderResponse.StatusCode);

        using var invalidExternalIdResponse = await client.PostAsJsonAsync(
            MappingsRoute,
            new ExternalIdentityMappingRequest("twitch", " ", identityId),
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidExternalIdResponse.StatusCode);

        var unknownIdentity = Guid.NewGuid();
        using var unknownIdentityResponse = await client.PostAsJsonAsync(
            MappingsRoute,
            new ExternalIdentityMappingRequest("twitch", "unknown", unknownIdentity),
            TestToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownIdentityResponse.StatusCode);
        Assert.Equal("application/problem+json", unknownIdentityResponse.Content.Headers.ContentType?.MediaType);

        var first = await LinkAsync(client, identityId, "reassignment");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var secondIdentity = await CreateIdentityAsync(client);
        using var conflictResponse = await client.PostAsJsonAsync(
            MappingsRoute,
            new ExternalIdentityMappingRequest("twitch", "reassignment", secondIdentity),
            TestToken);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var problem = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>(TestToken);
        Assert.Equal("External-Identity-Mapping-Konflikt.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnknownMappingsAndInvalidRouteIdsReturnNotFoundOrBadRequest()
    {
        SkipIfUnavailable();
        await ResetDatabaseAsync();

        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var unknown = await client.GetAsync(
            $"{MappingsRoute}/twitch/missing",
            TestToken);
        using var invalidCommunity = await client.GetAsync(
            $"{MappingsRoute}/community/not-a-guid",
            TestToken);
        using var unknownDelete = await client.DeleteAsync(
            $"{MappingsRoute}/twitch/missing",
            TestToken);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCommunity.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownDelete.StatusCode);
    }

    private async Task<Guid> CreateIdentityAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/identities", null, TestToken);
        var body = await response.Content.ReadFromJsonAsync<CreateCommunityIdentityResponse>(TestToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        return body!.Id;
    }

    private static async Task<(HttpStatusCode StatusCode, ExternalIdentityMappingResponse? Body)> LinkAsync(
        HttpClient client,
        Guid communityIdentityId,
        string externalUserId)
    {
        using var response = await client.PostAsJsonAsync(
            MappingsRoute,
            new ExternalIdentityMappingRequest("twitch", externalUserId, communityIdentityId),
            TestToken);
        var body = await response.Content.ReadFromJsonAsync<ExternalIdentityMappingResponse>(TestToken);
        return (response.StatusCode, body);
    }

    private async Task ResetDatabaseAsync() =>
        await database.ResetDatabaseAsync(TestToken);

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestToken);
        return connection;
    }

    private void SkipIfUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
