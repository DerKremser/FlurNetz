using Testcontainers.PostgreSql;

namespace FlurNetz.Modules.Titles.IntegrationTests;

/// <summary>
/// Verwaltet eine isolierte PostgreSQL-Testdatenbank für den Titles-Vertical-Slice.
/// </summary>
/// <remarks>
/// Die Integrationstests verwenden echtes PostgreSQL, damit Composite Keys, Foreign Keys,
/// Zeilensperren und Transaktionsverhalten nicht durch SQLite oder eine In-Memory-Alternative
/// verdeckt werden. Ohne Docker kann <c>FLURNETZ_TEST_CONNECTION_STRING</c> verwendet werden.
/// </remarks>
public sealed class TitlesPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;

    public bool IsAvailable { get; private set; }

    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";

    public string ConnectionString => connectionString
        ?? throw new InvalidOperationException("The PostgreSQL test infrastructure is unavailable.");

    public async ValueTask InitializeAsync()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable("FLURNETZ_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            connectionString = configuredConnectionString;
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

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("docker", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not installed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not running", StringComparison.OrdinalIgnoreCase)
                || message.Contains("daemon", StringComparison.OrdinalIgnoreCase)
                || message.Contains("cannot connect", StringComparison.OrdinalIgnoreCase));
    }
}
