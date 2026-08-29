using FlurNetz.Persistence.Configuration;
using Npgsql;

namespace FlurNetz.Persistence.Connections;

/// <summary>
/// Öffnet PostgreSQL-Verbindungen über eine gemeinsam genutzte Npgsql-Datenquelle.
/// </summary>
/// <remarks>
/// Die Datenquelle verwaltet den Verbindungspool. Einzelne geöffnete Verbindungen
/// bleiben trotzdem im Besitz des jeweiligen Aufrufers und müssen von diesem entsorgt werden.
/// </remarks>
public sealed class PostgreSqlConnectionFactory : IPostgreSqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>
    /// Erstellt eine Datenquelle aus der validierten PostgreSQL-Konfiguration.
    /// </summary>
    /// <param name="options">Die PostgreSQL-Konfiguration.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="options"/> fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn die Konfiguration ungültig ist.</exception>
    public PostgreSqlConnectionFactory(PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        dataSource = new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
    }

    /// <summary>
    /// Erstellt eine Datenquelle aus einer PostgreSQL-Verbindungszeichenfolge.
    /// </summary>
    /// <param name="connectionString">Die Verbindungszeichenfolge.</param>
    public PostgreSqlConnectionFactory(string connectionString)
        : this(new PostgreSqlOptions(connectionString))
    {
    }

    /// <summary>
    /// Gibt die gemeinsam genutzte Npgsql-Datenquelle für fortgeschrittene Infrastruktur-Integrationen zurück.
    /// </summary>
    public NpgsqlDataSource DataSource => dataSource;

    /// <summary>
    /// Öffnet eine Verbindung aus dem Pool der Datenquelle.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen des Öffnens.</param>
    /// <returns>Eine geöffnete PostgreSQL-Verbindung.</returns>
    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        return dataSource.OpenConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Gibt die Npgsql-Datenquelle und ihren Verbindungspool asynchron frei.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return dataSource.DisposeAsync();
    }
}
