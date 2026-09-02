using Testcontainers.PostgreSql;

namespace FlurNetz.Modules.Overlay.IntegrationTests;

/// <summary>Stellt PostgreSQL aus der vorhandenen Testcontainers-Konvention bereit.</summary>
public sealed class OverlayPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;

    /// <summary>Gibt an, ob PostgreSQL verfügbar ist.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Grund für einen übersprungenen Test.</summary>
    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";

    /// <summary>Verbindungsstring der isolierten Testdatenbank.</summary>
    public string ConnectionString => connectionString ?? throw new InvalidOperationException(SkipReason);

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
        catch (Exception exception) when (exception.ToString().Contains("docker", StringComparison.OrdinalIgnoreCase))
        {
            SkipReason = "Docker is unavailable; set FLURNETZ_TEST_CONNECTION_STRING to a PostgreSQL test database to run these tests.";
            if (container is not null) await container.DisposeAsync().ConfigureAwait(false);
            container = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync().ConfigureAwait(false);
    }
}
