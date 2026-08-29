using Npgsql;

namespace FlurNetz.Persistence.Connections;

public interface IPostgreSqlConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
