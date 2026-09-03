using FlurNetz.Api;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Startet den echten API-Host mit einer für den Testcontainer überschriebenen Konfiguration.
/// </summary>
public sealed class FlurNetzApiFactory(string connectionString, bool enableAdmin = false) : WebApplicationFactory<Program>
{
    public const string TestAdminLoginName = "TestAdmin";
    public const string TestAdminPassword = "test-admin-sentinel-passphrase-123";
    public static readonly Guid TestAdminIdentityId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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
                values["Administration:Bootstrap:CommunityIdentityId"] = TestAdminIdentityId.ToString("D");
                values["Administration:Bootstrap:LoginName"] = TestAdminLoginName;
                values["Administration:Bootstrap:InitialPassword"] = TestAdminPassword;
            }

            configuration.AddInMemoryCollection(values);
        });

        if (enableAdmin)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<AdminBootstrapper>();
                services.AddScoped<IAdminBootstrapper, TestAdminBootstrapper>();
            });
        }
    }

    public async Task<HttpClient> CreateAdminClientAsync(CancellationToken cancellationToken = default)
    {
        if (!enableAdmin)
        {
            throw new InvalidOperationException("The factory must be created with enableAdmin=true.");
        }

        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
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
                new KeyValuePair<string, string>("LoginName", TestAdminLoginName),
                new KeyValuePair<string, string>("Password", TestAdminPassword),
                new KeyValuePair<string, string>("ReturnUrl", "/admin")
            ]),
            cancellationToken).ConfigureAwait(false);
        if (!loginResponse.IsSuccessStatusCode)
        {
            var body = await loginResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            client.Dispose();
            throw new InvalidOperationException($"The test admin login failed with {(int)loginResponse.StatusCode}: {body}");
        }

        var authenticatedPage = await client.GetStringAsync("/admin/account", cancellationToken).ConfigureAwait(false);
        var authenticatedToken = ExtractAntiforgeryToken(authenticatedPage);
        if (authenticatedToken is null)
        {
            client.Dispose();
            throw new InvalidOperationException("The authenticated admin page did not expose an antiforgery token.");
        }

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", authenticatedToken);
        return client;
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        var tokenMatch = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
    }
}

internal sealed class TestAdminBootstrapper(
    AdminBootstrapper inner,
    ICommunityIdentityRepository identityRepository) : IAdminBootstrapper
{
    public async Task<bool> BootstrapAsync(AdminBootstrapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var identityId = CommunityIdentityId.Create(FlurNetzApiFactory.TestAdminIdentityId);
        if (await identityRepository.GetByIdAsync(identityId, cancellationToken).ConfigureAwait(false) is null)
        {
            await identityRepository.AddAsync(CommunityIdentity.Create(identityId), cancellationToken).ConfigureAwait(false);
        }

        return await inner.BootstrapAsync(configuration, cancellationToken).ConfigureAwait(false);
    }
}
