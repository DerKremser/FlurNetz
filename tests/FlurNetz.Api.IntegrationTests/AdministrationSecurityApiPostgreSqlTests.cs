using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

public sealed class AdministrationSecurityApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task AnonymousAdminPageRedirectsButAdminApiReturnsUnauthorized()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var page = await client.GetAsync("/admin", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/admin/login", page.Headers.Location?.ToString(), StringComparison.Ordinal);

        using var api = await client.GetAsync("/api/admin/shop/offers", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task LoginIsGenericCsrfProtectedAndLogoutIsPostOnly()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await ReadAntiforgeryTokenAsync(client, "/admin/login", TestContext.Current.CancellationToken);

        using var missingCsrf = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
        [
            new("Email", FlurNetzApiFactory.TestAdminEmail),
            new("Password", FlurNetzApiFactory.TestAdminPassword)
        ]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var badLogin = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("Email", "unknown-admin@example.com"),
            new("Password", "this is an invalid password")
        ]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, badLogin.StatusCode);
        var badBody = await badLogin.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Anmeldedaten sind ungültig.", WebUtility.HtmlDecode(badBody), StringComparison.Ordinal);
        Assert.DoesNotContain(FlurNetzApiFactory.TestAdminPassword, badBody, StringComparison.Ordinal);

        using var getLogout = await client.GetAsync("/admin/logout", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getLogout.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdminMutationRequiresCsrfAndCredentialVersionRevokesCookie()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await factory.CreateAdminClientAsync(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        using var csrfMissing = await client.PostAsJsonAsync(
            "/api/admin/shop/offers",
            new { ItemDefinitionId = Guid.NewGuid(), DisplayName = "csrf test", Price = 10 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, csrfMissing.StatusCode);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand(
                "UPDATE administration_credentials SET credential_version = credential_version + 1 WHERE community_identity_id = @Id;",
                connection);
            command.Parameters.AddWithValue("Id", await factory.GetTestAdminIdentityIdAsync(TestContext.Current.CancellationToken));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using var revoked = await client.GetAsync("/api/admin/shop/offers", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimitTemporarilyBlocksRepeatedFailures()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await ReadAntiforgeryTokenAsync(client, "/admin/login", TestContext.Current.CancellationToken);
        HttpStatusCode last = HttpStatusCode.OK;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("Email", "unknown-admin@example.com"),
                new("Password", "this is an invalid password")
            ]), TestContext.Current.CancellationToken);
            last = response.StatusCode;
        }

        Assert.Equal((HttpStatusCode)429, last);
    }

    [Fact]
    public async Task FirstRunSetupIsAvailableOnceAndAccountRetainsTheGenerator()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var anonymousSetup = await anonymous.GetAsync("/admin/setup", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, anonymousSetup.StatusCode);
        Assert.Equal("no-store", anonymousSetup.Headers.CacheControl?.ToString());
        var setupBody = await anonymousSetup.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("E-Mail-Adresse", setupBody, StringComparison.Ordinal);
        Assert.Contains("Sicheres Passwort generieren", setupBody, StringComparison.Ordinal);

        using var client = await factory.CreateAdminClientAsync(TestContext.Current.CancellationToken);
        using var closedSetup = await client.GetAsync("/admin/setup", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, closedSetup.StatusCode);

        using var account = await client.GetAsync("/admin/account", TestContext.Current.CancellationToken);
        var accountBody = await account.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains("Sicheres Passwort generieren", accountBody, StringComparison.Ordinal);
        Assert.Contains("data-password-generator", accountBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.random", accountBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstRunSetupRejectsMissingOrWrongGateWithoutPersistingSensitiveValues()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetDatabaseAsync(cancellationToken);

        using (var missingGateFactory = new FlurNetzApiFactory(database.ConnectionString))
        using (var missingGateClient = missingGateFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
        {
            using var missingCsrf = await missingGateClient.PostAsync(
                "/admin/setup",
                new FormUrlEncodedContent(
                [
                    new("Email", "missing-csrf@example.com"),
                    new("NewPassword", "sentinel missing-csrf password"),
                    new("NewPasswordConfirmation", "sentinel missing-csrf password"),
                    new("SetupSecret", FlurNetzApiFactory.TestAdminSetupSecret)
                ]),
                cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
            await AssertNoFirstRunSensitiveStateAsync(cancellationToken);

            var token = await ReadAntiforgeryTokenAsync(missingGateClient, "/admin/setup", cancellationToken);
            using var response = await missingGateClient.PostAsync(
                "/admin/setup",
                new FormUrlEncodedContent(
                [
                    new("__RequestVerificationToken", token),
                    new("Email", "missing-gate@example.com"),
                    new("NewPassword", "sentinel missing-gate password"),
                    new("NewPasswordConfirmation", "sentinel missing-gate password"),
                    new("SetupSecret", FlurNetzApiFactory.TestAdminSetupSecret)
                ]),
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Die Ersteinrichtung konnte nicht abgeschlossen werden.", WebUtility.HtmlDecode(body), StringComparison.Ordinal);
            Assert.DoesNotContain("sentinel missing-gate password", body, StringComparison.Ordinal);
            Assert.DoesNotContain(FlurNetzApiFactory.TestAdminSetupSecret, body, StringComparison.Ordinal);
            await AssertNoFirstRunSensitiveStateAsync(cancellationToken);
        }

        await database.ResetDatabaseAsync(cancellationToken);
        using var wrongGateFactory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var wrongGateClient = wrongGateFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var wrongGateToken = await ReadAntiforgeryTokenAsync(wrongGateClient, "/admin/setup", cancellationToken);
        using var wrongGateResponse = await wrongGateClient.PostAsync(
            "/admin/setup",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", wrongGateToken),
                new("Email", "wrong-gate@example.com"),
                new("NewPassword", "sentinel wrong-gate password"),
                new("NewPasswordConfirmation", "sentinel wrong-gate password"),
                new("SetupSecret", "wrong setup gate")
            ]),
            cancellationToken);
        var wrongGateBody = await wrongGateResponse.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, wrongGateResponse.StatusCode);
        Assert.Contains("Die Ersteinrichtung konnte nicht abgeschlossen werden.", WebUtility.HtmlDecode(wrongGateBody), StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel wrong-gate password", wrongGateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong setup gate", wrongGateBody, StringComparison.Ordinal);
        await AssertNoFirstRunSensitiveStateAsync(cancellationToken);
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        var body = await client.GetStringAsync(path, cancellationToken);
        var match = Regex.Match(body, "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The page {path} did not expose an antiforgery token.");
        return match.Groups[1].Value;
    }

    private async Task AssertNoFirstRunSensitiveStateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM community_identities;", cancellationToken: cancellationToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_credentials;", cancellationToken: cancellationToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_role_assignments;", cancellationToken: cancellationToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_audit_entries;", cancellationToken: cancellationToken)));
        Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_operations;", cancellationToken: cancellationToken)));
    }

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
}
