using Dapper;
using FlurNetz.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Startet den echten API-Host mit einer für den Testcontainer überschriebenen Konfiguration.
/// </summary>
public sealed class FlurNetzApiFactory(string connectionString, bool enableAdmin = false) : WebApplicationFactory<Program>
{
    public const string TestAdminEmail = "test-admin@example.com";
    public const string TestAdminSetupSecret = "test-admin-setup-secret";
    public const string TestAdminPassword = "test-admin-sentinel-passphrase-123";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlurNetz"] = connectionString
            };
            if (enableAdmin)
            {
                values["Administration:Setup:Secret"] = TestAdminSetupSecret;
            }

            configuration.AddInMemoryCollection(values);
        });

    }

    public async Task<HttpClient> CreateAdminClientAsync(
        CancellationToken cancellationToken = default,
        bool allowAutoRedirect = true)
    {
        if (!enableAdmin)
        {
            throw new InvalidOperationException("The factory must be created with enableAdmin=true.");
        }

        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });
        await SetupTestAdminAsync(client, connectionString, cancellationToken, allowAutoRedirect).ConfigureAwait(false);
        var loginPage = await client.GetStringAsync("/admin/login", cancellationToken).ConfigureAwait(false);
        var loginToken = ExtractAntiforgeryToken(loginPage);
        if (loginToken is null)
        {
            client.Dispose();
            throw new InvalidOperationException("The admin login page did not expose an antiforgery token.");
        }

        using var loginResponse = await client.PostAsync(
            "/admin/login",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
                new KeyValuePair<string, string>("Email", TestAdminEmail),
                new KeyValuePair<string, string>("Password", TestAdminPassword),
                new KeyValuePair<string, string>("ReturnUrl", "/admin")
            ]),
            cancellationToken).ConfigureAwait(false);
        if (!loginResponse.IsSuccessStatusCode
            && (allowAutoRedirect || !IsRedirect(loginResponse.StatusCode)))
        {
            var body = await loginResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            client.Dispose();
            throw new InvalidOperationException($"The test admin login failed with {(int)loginResponse.StatusCode}: {body}");
        }

        var authenticatedPage = await client.GetStringAsync("/admin/account", cancellationToken).ConfigureAwait(false);
        if (!WebUtility.HtmlDecode(authenticatedPage).Contains("Passwort ändern", StringComparison.Ordinal))
        {
            client.Dispose();
            throw new InvalidOperationException($"The test admin login did not produce an authenticated account page. Final response excerpt: {authenticatedPage[..Math.Min(authenticatedPage.Length, 180)]}");
        }
        var authenticatedToken = ExtractAntiforgeryToken(authenticatedPage);
        if (authenticatedToken is null)
        {
            client.Dispose();
            throw new InvalidOperationException("The authenticated admin page did not expose an antiforgery token.");
        }

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", authenticatedToken);
        return client;
    }

    public async Task<Guid> GetTestAdminIdentityIdAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<Guid>(new Dapper.CommandDefinition(
            "SELECT community_identity_id FROM administration_credentials WHERE normalized_email = @Email;",
            new { Email = TestAdminEmail.ToUpperInvariant() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task SetupTestAdminAsync(
        HttpClient client,
        string connectionString,
        CancellationToken cancellationToken,
        bool allowAutoRedirect)
    {
        using var setupPage = await client.GetAsync("/admin/setup", cancellationToken).ConfigureAwait(false);
        if (setupPage.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        setupPage.EnsureSuccessStatusCode();
        var setupBody = await setupPage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var setupToken = ExtractAntiforgeryToken(setupBody)
            ?? throw new InvalidOperationException("The first-run setup page did not expose an antiforgery token.");
        using var setupResponse = await client.PostAsync(
            "/admin/setup",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", setupToken),
                new KeyValuePair<string, string>("Email", TestAdminEmail),
                new KeyValuePair<string, string>("NewPassword", TestAdminPassword),
                new KeyValuePair<string, string>("NewPasswordConfirmation", TestAdminPassword),
                new KeyValuePair<string, string>("SetupSecret", TestAdminSetupSecret)
            ]),
            cancellationToken).ConfigureAwait(false);
        if (!setupResponse.IsSuccessStatusCode
            && (allowAutoRedirect || !IsRedirect(setupResponse.StatusCode)))
        {
            setupResponse.EnsureSuccessStatusCode();
        }
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var credentialCount = await connection.ExecuteScalarAsync<int>(new Dapper.CommandDefinition(
            "SELECT count(*) FROM administration_credentials WHERE normalized_email = @Email;",
            new { Email = TestAdminEmail.ToUpperInvariant() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (credentialCount != 1)
        {
            throw new InvalidOperationException($"The first-run setup response succeeded but did not create the test admin credential (count: {credentialCount}).");
        }
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        var tokenMatch = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and < 400;
}
