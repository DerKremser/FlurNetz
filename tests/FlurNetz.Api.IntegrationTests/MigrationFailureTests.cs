namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft, dass ein nicht erreichbarer PostgreSQL-Startup den API-Host nicht betriebsbereit macht.
/// </summary>
public sealed class MigrationFailureTests
{
    [Fact]
    public void HostStartupFailsWhenPostgreSqlIsUnavailable()
    {
        using var factory = new FlurNetzApiFactory(
            "Host=127.0.0.1;Port=1;Database=postgres;Username=postgres;Timeout=1;Command Timeout=1");

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Npgsql", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
