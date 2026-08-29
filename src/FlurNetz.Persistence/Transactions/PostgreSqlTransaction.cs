using FlurNetz.Persistence.Connections;
using Npgsql;

namespace FlurNetz.Persistence.Transactions;

/// <summary>
/// Owns one PostgreSQL connection and its transaction.
/// </summary>
public sealed class PostgreSqlTransaction : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private bool completed;
    private bool disposed;

    private PostgreSqlTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    public NpgsqlConnection Connection
    {
        get
        {
            ThrowIfDisposed();
            return connection;
        }
    }

    public NpgsqlTransaction Transaction
    {
        get
        {
            ThrowIfDisposed();
            return transaction;
        }
    }

    public static async ValueTask<PostgreSqlTransaction> BeginAsync(
        IPostgreSqlConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        NpgsqlConnection? connection = null;
        try
        {
            connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new PostgreSqlTransaction(connection, transaction);
        }
        catch
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfCompleted();

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfCompleted();

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (!completed)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                completed = true;
            }
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                disposed = true;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private void ThrowIfCompleted()
    {
        if (completed)
        {
            throw new InvalidOperationException("The PostgreSQL transaction has already completed.");
        }
    }
}
