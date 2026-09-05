using Dapper;
using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Application;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

public sealed class LocalizationUiTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task AdminLoginDefaultsToGermanAndPersistsTheSelectedCulture()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var german = await client.GetAsync("/admin/login", TestContext.Current.CancellationToken);
        var germanBody = await german.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, german.StatusCode);
        Assert.Contains("<html lang=\"de\">", germanBody, StringComparison.Ordinal);
        Assert.Contains(">Anmelden<", germanBody, StringComparison.Ordinal);
        Assert.Contains(">Deutsch<", germanBody, StringComparison.Ordinal);

        using var switched = await client.GetAsync(
            "/admin/culture?culture=en&returnUrl=%2Fadmin%2Flogin",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, switched.StatusCode);
        Assert.Equal("/admin/login", switched.Headers.Location?.OriginalString);
        Assert.Contains(
            ".AspNetCore.Culture=",
            string.Join(";", switched.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);

        using var english = await client.GetAsync("/admin/login", TestContext.Current.CancellationToken);
        var englishBody = await english.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<html lang=\"en\">", englishBody, StringComparison.Ordinal);
        Assert.Contains(">Sign in<", englishBody, StringComparison.Ordinal);
        Assert.Contains(">English<", englishBody, StringComparison.Ordinal);

        var token = Regex.Match(
            englishBody,
            "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var invalidLogin = await client.PostAsync(
            "/admin/login",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("Email", string.Empty),
                new("Password", string.Empty),
                new("ReturnUrl", "/admin")
            ]),
            TestContext.Current.CancellationToken);
        var invalidLoginBody = WebUtility.HtmlDecode(
            await invalidLogin.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, invalidLogin.StatusCode);
        Assert.Contains("Email address is required.", invalidLoginBody, StringComparison.Ordinal);
        Assert.Contains("Password is required.", invalidLoginBody, StringComparison.Ordinal);

        using var invalid = await client.GetAsync(
            "/admin/culture?culture=fr&returnUrl=%2Fadmin%2Flogin",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdminShellUsesGermanLabelsWithoutGlobalCultureSwitcher()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await factory.CreateAdminClientAsync(TestContext.Current.CancellationToken);

        using var account = await client.GetAsync("/admin/account", TestContext.Current.CancellationToken);
        var body = await account.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var decodedBody = WebUtility.HtmlDecode(body);
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains("<html lang=\"de\">", body, StringComparison.Ordinal);
        Assert.Contains("Passwort ändern", decodedBody, StringComparison.Ordinal);
        Assert.Contains("Übersicht", decodedBody, StringComparison.Ordinal);
        Assert.Contains("Abmelden", decodedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"language-switcher\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/admin/culture\"", body, StringComparison.Ordinal);

        using var authenticatedCulture = await client.GetAsync(
            "/admin/culture?culture=en&returnUrl=%2Fadmin%2Faccount",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, authenticatedCulture.StatusCode);

        foreach (var path in new[]
        {
            "/admin",
            "/admin/identities",
            "/admin/shop",
            "/admin/catalog",
            "/admin/automation",
            "/admin/integrations",
            "/admin/overlay",
            "/admin/audit"
        })
        {
            using var page = await client.GetAsync(path, TestContext.Current.CancellationToken);
            var pageBody = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("<html lang=\"de\">", pageBody, StringComparison.Ordinal);
            Assert.Contains("Übersicht", WebUtility.HtmlDecode(pageBody), StringComparison.Ordinal);
            Assert.DoesNotContain("Layout_", pageBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdministratorPreferredCultureSurvivesSaveSessionResetAndLogin()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetDatabaseAsync(cancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await factory.CreateAdminClientAsync(cancellationToken, allowAutoRedirect: false);

        var initialBody = await client.GetStringAsync("/admin/account", cancellationToken);
        Assert.Contains("<html lang=\"de\">", initialBody, StringComparison.Ordinal);
        Assert.Contains("option value=\"de\" selected", initialBody, StringComparison.Ordinal);

        var savedEnglish = await SaveLanguageAsync(client, "en", cancellationToken);
        Assert.Contains("<html lang=\"en\">", savedEnglish, StringComparison.Ordinal);
        Assert.Contains("Your preferred language was saved.", savedEnglish, StringComparison.Ordinal);
        Assert.Contains("option value=\"en\" selected", savedEnglish, StringComparison.Ordinal);
        Assert.Equal("en", await ReadPreferredCultureAsync(FlurNetzApiFactory.TestAdminEmail, TestContext.Current.CancellationToken));

        var invalidToken = ExtractAntiforgeryToken(savedEnglish);
        Assert.NotNull(invalidToken);
        using var invalid = await client.PostAsync(
            "/admin/account?handler=Language",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", invalidToken!),
                new("PreferredCulture", "fr")
            ]),
            cancellationToken);
        var invalidBody = await invalid.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Please choose Deutsch or English.", invalidBody, StringComparison.Ordinal);
        Assert.Equal("en", await ReadPreferredCultureAsync(FlurNetzApiFactory.TestAdminEmail, cancellationToken));

        using var freshClient = await LoginFreshAsync(
            factory,
            FlurNetzApiFactory.TestAdminEmail,
            FlurNetzApiFactory.TestAdminPassword,
            cancellationToken);
        using var afterFreshLogin = await freshClient.GetAsync("/admin/account", cancellationToken);
        var afterFreshLoginBody = await afterFreshLogin.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, afterFreshLogin.StatusCode);
        Assert.Contains("<html lang=\"en\">", afterFreshLoginBody, StringComparison.Ordinal);
        Assert.Contains("Change password", afterFreshLoginBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoAdministratorsKeepIndependentPreferredCultures()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetDatabaseAsync(cancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var adminA = await factory.CreateAdminClientAsync(cancellationToken, allowAutoRedirect: false);

        using var identityResponse = await adminA.PostAsync("/api/identities", content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, identityResponse.StatusCode);
        var identity = await identityResponse.Content.ReadFromJsonAsync<CreateCommunityIdentityResponse>(cancellationToken);
        Assert.NotNull(identity);
        const string adminBEmail = "second-admin@example.com";
        const string adminBPassword = "second-admin-sentinel-passphrase-123";
        await SeedAdminAsync(identity!.Id, adminBEmail, adminBPassword, cancellationToken);

        var adminABody = await SaveLanguageAsync(adminA, "en", cancellationToken);
        Assert.Contains("<html lang=\"en\">", adminABody, StringComparison.Ordinal);

        using var adminB = await LoginFreshAsync(factory, adminBEmail, adminBPassword, cancellationToken);
        using var adminBInitial = await adminB.GetAsync("/admin/account", cancellationToken);
        var adminBInitialBody = await adminBInitial.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, adminBInitial.StatusCode);
        Assert.Contains("<html lang=\"de\">", adminBInitialBody, StringComparison.Ordinal);

        var adminBBody = await SaveLanguageAsync(adminB, "en", cancellationToken);
        Assert.Contains("<html lang=\"en\">", adminBBody, StringComparison.Ordinal);
        Assert.Equal("en", await ReadPreferredCultureAsync(adminBEmail, cancellationToken));
        Assert.Equal("en", await ReadPreferredCultureAsync(FlurNetzApiFactory.TestAdminEmail, cancellationToken));

        var adminABackToGerman = await SaveLanguageAsync(adminA, "de", cancellationToken);
        Assert.Contains("<html lang=\"de\">", adminABackToGerman, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en\">", await adminB.GetStringAsync("/admin/account", cancellationToken), StringComparison.Ordinal);

        using var adminAFreshLogin = await LoginFreshAsync(
            factory,
            FlurNetzApiFactory.TestAdminEmail,
            FlurNetzApiFactory.TestAdminPassword,
            cancellationToken);
        Assert.Contains("<html lang=\"de\">", await adminAFreshLogin.GetStringAsync("/admin/account", cancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DashboardAndAuditLocalizeKnownValuesAndFallbackUnknownValues()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetDatabaseAsync(cancellationToken);
        using var factory = new FlurNetzApiFactory(database.ConnectionString, enableAdmin: true);
        using var client = await factory.CreateAdminClientAsync(cancellationToken, allowAutoRedirect: false);

        await SaveLanguageAsync(client, "de", cancellationToken);
        var germanDashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/admin", cancellationToken));
        Assert.Contains("Identität nachschlagen", germanDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Identity Lookup", germanDashboard, StringComparison.Ordinal);
        Assert.Contains("Kontostände, EP und Benachrichtigungen", germanDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Balances, XP und Notifications", germanDashboard, StringComparison.Ordinal);

        await AppendUnknownAuditAsync(cancellationToken);
        var germanAudit = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/audit", cancellationToken));
        Assert.Contains("Spracheinstellung geändert", germanDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Administration.PreferredCultureChanged", germanDashboard, StringComparison.Ordinal);
        Assert.Contains("Administratorkonto", germanDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminCredential", germanDashboard, StringComparison.Ordinal);
        Assert.Contains("Spracheinstellung geändert", germanAudit, StringComparison.Ordinal);
        Assert.DoesNotContain("Administration.PreferredCultureChanged", germanAudit, StringComparison.Ordinal);
        Assert.Contains("Administratorkonto", germanAudit, StringComparison.Ordinal);
        Assert.DoesNotContain(">AdminCredential<", germanAudit, StringComparison.Ordinal);
        Assert.Contains("Future.Action", germanAudit, StringComparison.Ordinal);
        Assert.Contains("FutureResource", germanAudit, StringComparison.Ordinal);

        await SaveLanguageAsync(client, "en", cancellationToken);
        var englishDashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/admin", cancellationToken));
        Assert.Contains("Identity Lookup", englishDashboard, StringComparison.Ordinal);
        Assert.Contains("Balances, XP and notifications", englishDashboard, StringComparison.Ordinal);
        Assert.Contains("Language preference changed", englishDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Administration.PreferredCultureChanged", englishDashboard, StringComparison.Ordinal);
        Assert.Contains("Administrator account", englishDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminCredential", englishDashboard, StringComparison.Ordinal);

        var englishAudit = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/audit", cancellationToken));
        Assert.Contains("Language preference changed", englishAudit, StringComparison.Ordinal);
        Assert.Contains("Administrator account", englishAudit, StringComparison.Ordinal);
        Assert.Contains("Future.Action", englishAudit, StringComparison.Ordinal);
        Assert.Contains("FutureResource", englishAudit, StringComparison.Ordinal);
    }

    private static async Task<string> SaveLanguageAsync(HttpClient client, string culture, CancellationToken cancellationToken)
    {
        var accountBody = await client.GetStringAsync("/admin/account", cancellationToken);
        var token = ExtractAntiforgeryToken(accountBody);
        Assert.NotNull(token);
        using var response = await client.PostAsync(
            "/admin/account?handler=Language",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token!),
                new("PreferredCulture", culture)
            ]),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/account", response.Headers.Location?.OriginalString);
        Assert.Contains(
            ".AspNetCore.Culture=",
            string.Join(";", response.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
        return await client.GetStringAsync("/admin/account", cancellationToken);
    }

    private static async Task<HttpClient> LoginFreshAsync(
        FlurNetzApiFactory factory,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var loginBody = await client.GetStringAsync("/admin/login", cancellationToken);
        var token = ExtractAntiforgeryToken(loginBody);
        Assert.NotNull(token);
        using var response = await client.PostAsync(
            "/admin/login",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token!),
                new("Email", email),
                new("Password", password),
                new("ReturnUrl", "/admin")
            ]),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
        Assert.Contains(
            ".AspNetCore.Culture=",
            string.Join(";", response.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
        return client;
    }

    private async Task SeedAdminAsync(
        Guid identityId,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var hash = new AdminPasswordHasher().Hash(password);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO administration_credentials
                (community_identity_id, email, normalized_email, password_hash,
                 credential_version, created_at_utc, password_changed_at_utc, preferred_culture)
            VALUES
                (@IdentityId, @Email, @NormalizedEmail, @PasswordHash,
                 1, @Now, @Now, NULL);
            INSERT INTO administration_role_assignments
                (community_identity_id, role_name, created_at_utc)
            VALUES (@IdentityId, 'Administrator', @Now);
            """,
            new
            {
                IdentityId = identityId,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = hash,
                Now = now
            },
            cancellationToken: cancellationToken));
    }

    private async Task<string?> ReadPreferredCultureAsync(
        string email,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<string?>(new CommandDefinition(
            "SELECT preferred_culture FROM administration_credentials WHERE normalized_email = @NormalizedEmail;",
            new { NormalizedEmail = email.ToUpperInvariant() },
            cancellationToken: cancellationToken));
    }

    private async Task AppendUnknownAuditAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var identityId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            "SELECT community_identity_id FROM administration_credentials WHERE normalized_email = @NormalizedEmail;",
            new { NormalizedEmail = FlurNetzApiFactory.TestAdminEmail.ToUpperInvariant() },
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO administration_audit_entries
                (id, actor_community_identity_id, actor_identity_snapshot, action, target_type,
                 target_id, target_display_snapshot, risk_level, reason, result, occurred_at_utc,
                 correlation_id, request_id, failure_code, change_summary, metadata)
            VALUES
                (@Id, @ActorId, 'operator', 'Future.Action', 'FutureResource', 'future-id', NULL,
                 'Low', NULL, 'Succeeded', @OccurredAtUtc, 'future-correlation', NULL, NULL,
                 CAST(@EmptyJson AS jsonb), CAST(@EmptyJson AS jsonb));
            """,
            new
            {
                Id = Guid.NewGuid(),
                ActorId = identityId,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                EmptyJson = "{}"
            },
            cancellationToken: cancellationToken));
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        var tokenMatch = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
    }

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
}
