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
public sealed class CommunityIdentityRepository : ICommunityIdentityRepository, ICommunityIdentityRead
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

        await AddAsync(identity, transaction.Connection, transaction.Transaction, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(
        CommunityIdentity identity,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        return connection.ExecuteAsync(
            new CommandDefinition(
                InsertSql,
                new { Id = identity.Id.Value },
                transaction: transaction,
                cancellationToken: cancellationToken));
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

    public async Task<CommunityIdentitySummary?> GetAsync(
        CommunityIdentityId id,
        CancellationToken cancellationToken = default)
    {
        var identity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return identity is null ? null : new CommunityIdentitySummary(identity.Id);
    }

    public async Task<CommunityIdentityPage> ListAsync(
        CommunityIdentityId? after,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT id
                FROM community_identities
                WHERE (@AfterId IS NULL OR id > @AfterId)
                ORDER BY id
                LIMIT @Limit;
                """,
                new { AfterId = after?.Value, Limit = pageSize + 1 },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        var materialized = ids.ToArray();
        var hasMore = materialized.Length > pageSize;
        var visible = hasMore ? materialized[..pageSize] : materialized;
        var items = visible
            .Select(value => new CommunityIdentitySummary(CommunityIdentityId.Create(value)))
            .ToArray();
        return new CommunityIdentityPage(
            Array.AsReadOnly(items),
            hasMore && items.Length > 0 ? items[^1].CommunityIdentityId : null);
    }
}
