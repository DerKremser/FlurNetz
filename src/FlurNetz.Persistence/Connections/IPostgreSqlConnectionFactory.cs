using Npgsql;

namespace FlurNetz.Persistence.Connections;

/// <summary>
/// Definiert den Erzeugungs- und Öffnungsvertrag für PostgreSQL-Verbindungen.
/// </summary>
/// <remarks>
/// Die Abstraktion hält die Persistence-Komponenten von einer konkreten Pool- oder
/// Datenquellenverwaltung fern und macht sie zugleich für Tests ersetzbar.
/// </remarks>
public interface IPostgreSqlConnectionFactory
{
    /// <summary>
    /// Öffnet eine Verbindung aus der konfigurierten PostgreSQL-Datenquelle.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen des asynchronen Verbindungsaufbaus.</param>
    /// <returns>Eine geöffnete Npgsql-Verbindung, deren Aufrufer die Lebensdauer übernimmt.</returns>
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
