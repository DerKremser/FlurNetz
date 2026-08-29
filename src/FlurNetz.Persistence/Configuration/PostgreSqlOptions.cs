using Npgsql;

namespace FlurNetz.Persistence.Configuration;

/// <summary>
/// Beschreibt die Verbindungsdaten, die die PostgreSQL-Persistence-Foundation benötigt.
/// </summary>
/// <remarks>
/// Die Optionen bündeln die Konfiguration an einer Stelle, damit die Infrastruktur
/// ohne hart codierte Zugangsdaten erzeugt und vor dem Aufbau des Datenpools validiert
/// werden kann.
/// </remarks>
public sealed class PostgreSqlOptions
{
    /// <summary>
    /// Initialisiert leere Optionen, wie sie beispielsweise von einem Konfigurationsbinder benötigt werden.
    /// </summary>
    public PostgreSqlOptions()
    {
    }

    /// <summary>
    /// Initialisiert die Optionen mit einer PostgreSQL-Verbindungszeichenfolge.
    /// </summary>
    /// <param name="connectionString">Die Verbindungszeichenfolge ohne Geheimnisse im Quelltext.</param>
    public PostgreSqlOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Gibt die PostgreSQL-Verbindungszeichenfolge zurück oder legt sie fest.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Prüft, ob die Verbindungszeichenfolge vorhanden und syntaktisch für Npgsql gültig ist.
    /// </summary>
    /// <exception cref="ArgumentException">Wird ausgelöst, wenn die Verbindungszeichenfolge fehlt oder ungültig ist.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(ConnectionString));
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(ConnectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new ArgumentException("The PostgreSQL connection string is invalid.", nameof(ConnectionString), exception);
        }
    }
}
