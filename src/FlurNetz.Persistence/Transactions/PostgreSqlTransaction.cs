using FlurNetz.Persistence.Connections;
using Npgsql;

namespace FlurNetz.Persistence.Transactions;

/// <summary>
/// Kapselt eine PostgreSQL-Verbindung und genau deren Transaktion.
/// </summary>
/// <remarks>
/// Die Kapselung stellt sicher, dass alle Befehle innerhalb einer Transaktionsgrenze
/// dieselbe Verbindung und dieselbe Transaktion verwenden. Der Besitzer dieses Objekts
/// entscheidet über Commit oder Rollback und gibt anschließend beide Ressourcen frei.
/// </remarks>
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

    /// <summary>
    /// Gibt die von dieser Transaktion besessene PostgreSQL-Verbindung zurück.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Wenn die Transaktion bereits freigegeben wurde.</exception>
    public NpgsqlConnection Connection
    {
        get
        {
            ThrowIfDisposed();
            return connection;
        }
    }

    /// <summary>
    /// Gibt die von dieser Kapselung besessene Datenbanktransaktion zurück.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Wenn die Transaktion bereits freigegeben wurde.</exception>
    public NpgsqlTransaction Transaction
    {
        get
        {
            ThrowIfDisposed();
            return transaction;
        }
    }

    /// <summary>
    /// Öffnet eine Verbindung und beginnt darauf eine PostgreSQL-Transaktion.
    /// </summary>
    /// <param name="connectionFactory">Fabrik, die die Verbindung bereitstellt.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Öffnens oder Beginnens.</param>
    /// <returns>Eine Transaktionskapsel, die Verbindung und Transaktion besitzt.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="connectionFactory"/> fehlt.</exception>
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
                // Bei einem Fehler vor der Rückgabe existiert noch kein Besitzer für die Verbindung.
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// Bestätigt die Transaktion und markiert sie als abgeschlossen.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen des Commit-Vorgangs.</param>
    /// <exception cref="ObjectDisposedException">Wenn die Kapselung bereits freigegeben wurde.</exception>
    /// <exception cref="InvalidOperationException">Wenn Commit oder Rollback bereits erfolgt ist.</exception>
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfCompleted();

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    /// <summary>
    /// Rollt die Transaktion zurück und markiert sie als abgeschlossen.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen des Rollback-Vorgangs.</param>
    /// <exception cref="ObjectDisposedException">Wenn die Kapselung bereits freigegeben wurde.</exception>
    /// <exception cref="InvalidOperationException">Wenn Commit oder Rollback bereits erfolgt ist.</exception>
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfCompleted();

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    /// <summary>
    /// Rollt eine noch offene Transaktion zurück und gibt danach Transaktion und Verbindung frei.
    /// </summary>
    /// <remarks>
    /// Der implizite Rollback schützt vor versehentlich offenen Transaktionen, wenn ein
    /// Aufrufer nach einem Fehler aus dem <c>await using</c>-Bereich fällt.
    /// </remarks>
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
                // Dispose darf keine unbestätigten Änderungen im Pool zurücklassen.
                await transaction.RollbackAsync().ConfigureAwait(false);
                completed = true;
            }
        }
        finally
        {
            try
            {
                // Die Transaktion wird vor der Verbindung freigegeben, weil sie von ihr abhängt.
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
