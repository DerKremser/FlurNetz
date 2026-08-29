using Npgsql;

namespace FlurNetz.Persistence.Configuration;

/// <summary>
/// Connection settings required by the PostgreSQL persistence foundation.
/// </summary>
public sealed class PostgreSqlOptions
{
    public PostgreSqlOptions()
    {
    }

    public PostgreSqlOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; init; } = string.Empty;

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
