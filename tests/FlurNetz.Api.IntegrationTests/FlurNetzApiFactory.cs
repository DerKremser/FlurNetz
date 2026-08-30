using FlurNetz.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Startet den echten API-Host mit einer für den Testcontainer überschriebenen Konfiguration.
/// </summary>
public sealed class FlurNetzApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlurNetz"] = connectionString
            }));
    }
}
