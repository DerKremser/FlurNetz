using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
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
        var token = await ReadAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        using var missingCsrf = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
        [
            new("LoginName", FlurNetzApiFactory.TestAdminLoginName),
            new("Password", FlurNetzApiFactory.TestAdminPassword)
        ]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var badLogin = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("LoginName", "unknown-admin"),
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
            command.Parameters.AddWithValue("Id", FlurNetzApiFactory.TestAdminIdentityId);
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
        var token = await ReadAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        HttpStatusCode last = HttpStatusCode.OK;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("LoginName", "unknown-admin"),
                new("Password", "this is an invalid password")
            ]), TestContext.Current.CancellationToken);
            last = response.StatusCode;
        }

        Assert.Equal((HttpStatusCode)429, last);
    }

    [Fact]
    public async Task PasswordGeneratorPagesRemainProtectedAndExposeOnlyTheFormAction()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var anonymousSetup = await anonymous.GetAsync("/admin/setup", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymousSetup.StatusCode);

        using var client = await factory.CreateAdminClientAsync(TestContext.Current.CancellationToken);
        foreach (var path in new[] { "/admin/account", "/admin/setup" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Sicheres Passwort generieren", body, StringComparison.Ordinal);
            Assert.Contains("data-password-generator", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Math.random", body, StringComparison.Ordinal);
        }
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var body = await client.GetStringAsync("/admin/login", cancellationToken);
        var match = Regex.Match(body, "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The login page did not expose an antiforgery token.");
        return match.Groups[1].Value;
    }

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
}
