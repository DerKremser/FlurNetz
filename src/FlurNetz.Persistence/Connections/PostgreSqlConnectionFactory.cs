using FlurNetz.Persistence.Configuration;
using Npgsql;

namespace FlurNetz.Persistence.Connections;

public sealed class PostgreSqlConnectionFactory : IPostgreSqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlConnectionFactory(PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        dataSource = new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
    }

    public PostgreSqlConnectionFactory(string connectionString)
        : this(new PostgreSqlOptions(connectionString))
    {
    }

    public NpgsqlDataSource DataSource => dataSource;

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        return dataSource.OpenConnectionAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return dataSource.DisposeAsync();
    }
}
