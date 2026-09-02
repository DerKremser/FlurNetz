using Testcontainers.PostgreSql;

namespace FlurNetz.Modules.Automation.IntegrationTests;

/// <summary>Stellt echtes PostgreSQL oder eine explizit konfigurierte Testdatenbank bereit.</summary>
public sealed class AutomationPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;

    /// <summary>Gibt an, ob PostgreSQL verfügbar ist.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Grund für einen übersprungenen Test.</summary>
    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";

    /// <summary>Verbindungsstring der isolierten Testdatenbank.</summary>
    public string ConnectionString => connectionString
        ?? throw new InvalidOperationException("The PostgreSQL test infrastructure is unavailable.");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("FLURNETZ_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            connectionString = configured;
            IsAvailable = true;
            return;
        }

        try
        {
            container = new PostgreSqlBuilder("postgres:15.1").Build();
            await container.StartAsync().ConfigureAwait(false);
            connectionString = container.GetConnectionString();
            IsAvailable = true;
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            SkipReason = "Docker is unavailable; set FLURNETZ_TEST_CONNECTION_STRING to a PostgreSQL test database to run these tests.";
            IsAvailable = false;
            if (container is not null)
            {
                await container.DisposeAsync().ConfigureAwait(false);
                container = null;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync().ConfigureAwait(false);
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        var text = exception.ToString();
        return text.Contains("docker", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || text.Contains("not installed", StringComparison.OrdinalIgnoreCase)
                || text.Contains("not running", StringComparison.OrdinalIgnoreCase)
                || text.Contains("daemon", StringComparison.OrdinalIgnoreCase)
                || text.Contains("cannot connect", StringComparison.OrdinalIgnoreCase));
    }
}
