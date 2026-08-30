using Dapper;
using System.Data.Common;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Economy.Persistence;

/// <summary>
/// Persistiert Community-Economy-Zustände mit atomaren, parametrisierten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Credit initialisiert ausschließlich im Credit-Pfad eine fehlende Zeile per
/// <c>ON CONFLICT DO NOTHING</c>. Beide Mutationen sperren anschließend die Zeile mit
/// <c>FOR UPDATE</c>, rehydrieren die Domain, führen deren fachliche Methode aus und
/// schreiben das Ergebnis innerhalb derselben Transaktion zurück. Debit legt bei einer
/// fehlenden Zeile bewusst keinen Nullzustand an. Der transaction-aware Credit-Overload
/// führt keinen Commit aus, damit ein aufrufendes Modul die gemeinsame Transaktionsgrenze
/// besitzt.
/// </remarks>
public sealed class CommunityEconomyStore : ICommunityEconomyStore
{
    private const string InitializeSql = """
        INSERT INTO community_economies
            (community_identity_id, balance)
        VALUES
            (@CommunityIdentityId, 0)
        ON CONFLICT (community_identity_id) DO NOTHING;
        """;

    private const string SelectForUpdateSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            balance AS Balance
        FROM community_economies
        WHERE community_identity_id = @CommunityIdentityId
        FOR UPDATE;
        """;

    private const string SelectSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            balance AS Balance
        FROM community_economies
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private const string UpdateSql = """
        UPDATE community_economies
        SET balance = @Balance
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Store mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    public CommunityEconomyStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await CreditAsync(
                communityIdentityId,
                amount,
                transaction.Connection,
                transaction.Transaction,
                cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        await connection.ExecuteAsync(
                new CommandDefinition(
                    InitializeSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var row = await connection.QuerySingleAsync<EconomyRow>(
                new CommandDefinition(
                    SelectForUpdateSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var economy = ToDomain(row);
        economy.Credit(amount);
        return await UpdateAsync(economy, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await DebitInTransactionAsync(
                communityIdentityId,
                amount,
                transaction.Connection,
                transaction.Transaction,
                cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommunityEconomy?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<EconomyRow>(
                new CommandDefinition(
                    SelectSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    private static async Task<EconomyBalance> DebitInTransactionAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        var row = await connection.QuerySingleOrDefaultAsync<EconomyRow>(
                new CommandDefinition(
                    SelectForUpdateSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var economy = row is null
            ? CommunityEconomy.Rehydrate(validCommunityIdentityId, EconomyBalance.Zero)
            : ToDomain(row);
        economy.Debit(amount);

        return await UpdateAsync(economy, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<EconomyBalance> UpdateAsync(
        CommunityEconomy economy,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var updatedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    UpdateSql,
                    new
                    {
                        CommunityIdentityId = economy.CommunityIdentityId.Value,
                        Balance = economy.Balance.Value
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                "Der Community-Economy-Zustand konnte nicht eindeutig aktualisiert werden.");
        }

        return economy.Balance;
    }

    private static CommunityEconomy ToDomain(EconomyRow row)
    {
        return CommunityEconomy.Rehydrate(
            CommunityIdentityId.Create(row.CommunityIdentityId),
            EconomyBalance.Create(row.Balance));
    }

    private sealed class EconomyRow
    {
        public Guid CommunityIdentityId { get; set; }

        public long Balance { get; set; }
    }
}
