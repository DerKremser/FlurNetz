using Testcontainers.PostgreSql;

namespace FlurNetz.Persistence.IntegrationTests;

/// <summary>
/// Verwaltet eine isolierte PostgreSQL-Testdatenbank für die Persistence-Integrationstests.
/// </summary>
/// <remarks>
/// Die Tests verwenden bewusst echtes PostgreSQL, weil Transaktions-, DDL- und Npgsql-Verhalten
/// nicht zuverlässig durch eine In-Memory- oder SQLite-Alternative abgebildet werden. Für lokale
/// Umgebungen ohne Docker kann eine separate Testdatenbank über <c>FLURNETZ_TEST_CONNECTION_STRING</c>
/// bereitgestellt werden.
/// </remarks>
public sealed class PostgreSqlFixture : IAsyncLifetime
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
            // Ein expliziter Testanschluss erlaubt die Ausführung auch ohne lokale Container-Runtime.
            connectionString = configuredConnectionString;
            IsAvailable = true;
            return;
        }

        try
        {
            // Testcontainers liefert pro Fixture eine echte, kurzlebige PostgreSQL-Instanz mit sauberer Isolation.
            container = new PostgreSqlBuilder("postgres:15.1").Build();
            await container.StartAsync().ConfigureAwait(false);
            connectionString = container.GetConnectionString();
            IsAvailable = true;
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            // Fehlende lokale Infrastruktur überspringt nur diese Tests; ein echter Datenbankfehler bleibt sichtbar.
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
            // Der Container wird auch nach erfolgreichen Tests beendet, damit keine Testdaten zurückbleiben.
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
