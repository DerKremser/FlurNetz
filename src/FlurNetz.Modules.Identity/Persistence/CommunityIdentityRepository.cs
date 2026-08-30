using Dapper;
using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Identity.Persistence;

/// <summary>
/// Persistiert Community-Identitäten mit gezieltem Dapper-SQL in PostgreSQL.
/// </summary>
/// <remarks>
/// Die fachliche Tabelle enthält bewusst nur den internen UUID-Schlüssel. Der Standard-
/// Schreibpfad besitzt seine Transaktion selbst; die transaktionsbewusste Überladung erlaubt
/// später eine atomare Komposition mit weiteren Identity-Schreibvorgängen.
/// </remarks>
public sealed class CommunityIdentityRepository : ICommunityIdentityRepository
{
    private const string InsertSql = """
        INSERT INTO community_identities (id)
        VALUES (@Id);
        """;

    private const string SelectByIdSql = """
        SELECT id
        FROM community_identities
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Repository-Adapter mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="connectionFactory"/> fehlt.</exception>
    public CommunityIdentityRepository(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(CommunityIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AddAsync(identity, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Speichert eine Identität innerhalb einer bereits begonnenen Identity-Transaktion.
    /// </summary>
    /// <param name="identity">Die bereits gültige interne Community-Identität.</param>
    /// <param name="transaction">Die gemeinsame PostgreSQL-Transaktion.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Ein Task nach Ausführung des INSERTs; der Aufrufer entscheidet über Commit oder Rollback.</returns>
    /// <exception cref="ArgumentNullException">Wenn Identität oder Transaktion fehlen.</exception>
    public async Task AddAsync(
        CommunityIdentity identity,
        PostgreSqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(transaction);

        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    InsertSql,
                    new { Id = identity.Id.Value },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CommunityIdentity?> GetByIdAsync(
        CommunityIdentityId id,
        CancellationToken cancellationToken = default)
    {
        var validId = CommunityIdentityId.Create(id.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var storedId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    SelectByIdSql,
                    new { Id = validId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return storedId is null
            ? null
            : CommunityIdentity.Create(CommunityIdentityId.Create(storedId.Value));
    }
}
