using Testcontainers.PostgreSql;

namespace FlurNetz.Worker.IntegrationTests;

/// <summary>
/// Verwaltet eine isolierte PostgreSQL-Testdatenbank für den echten Worker-Host.
/// </summary>
/// <remarks>
/// Die Tests prüfen die Laufzeitkomposition gegen echtes PostgreSQL. Ohne Docker kann eine
/// isolierte Testdatenbank über <c>FLURNETZ_TEST_CONNECTION_STRING</c> bereitgestellt werden.
/// </remarks>
public sealed class WorkerPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;

    /// <summary>
    /// Gibt an, ob die PostgreSQL-Testinfrastruktur verfügbar ist.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Beschreibt den Grund für einen übersprungenen PostgreSQL-Test.
    /// </summary>
    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";

    /// <summary>
    /// Gibt die isolierte Test-Verbindungszeichenfolge zurück.
    /// </summary>
    public string ConnectionString => connectionString
        ?? throw new InvalidOperationException("The PostgreSQL test infrastructure is unavailable.");

    /// <inheritdoc />
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

    /// <inheritdoc />
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
