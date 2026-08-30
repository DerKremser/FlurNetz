using Testcontainers.PostgreSql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Verwaltet eine isolierte PostgreSQL-Testdatenbank für den echten API-Slice.
/// </summary>
/// <remarks>
/// Die Fixture verwendet Testcontainers oder eine ausdrücklich konfigurierte isolierte
/// Datenbank. So prüft der Test den vollständigen Weg bis zum realen PostgreSQL-Adapter.
/// </remarks>
public sealed class ApiPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;

    /// <summary>
    /// Gibt an, ob eine echte PostgreSQL-Verbindung verfügbar ist.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Beschreibt, warum die PostgreSQL-Testinfrastruktur gegebenenfalls nicht verfügbar ist.
    /// </summary>
    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";

    /// <summary>
    /// Gibt die konfigurierte Test-Verbindungszeichenfolge zurück.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn keine Testdatenbank verfügbar ist.</exception>
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
