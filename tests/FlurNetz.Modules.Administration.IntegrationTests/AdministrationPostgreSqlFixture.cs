using Npgsql;
using Testcontainers.PostgreSql;

namespace FlurNetz.Modules.Administration.IntegrationTests;

public sealed class AdministrationPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? connectionString;
    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = "PostgreSQL test infrastructure is unavailable.";
    public string ConnectionString => connectionString ?? throw new InvalidOperationException(SkipReason);

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

    public async ValueTask DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync().ConfigureAwait(false);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            DROP TABLE IF EXISTS administration_operations;
            DROP TABLE IF EXISTS administration_audit_entries;
            DROP TABLE IF EXISTS administration_role_assignments;
            DROP TABLE IF EXISTS administration_credentials;
            DROP TABLE IF EXISTS administration_setup_state;
            DROP TABLE IF EXISTS community_identities;
            DROP SCHEMA IF EXISTS flurnetz_persistence CASCADE;
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
