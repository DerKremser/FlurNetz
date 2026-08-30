using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Inventory.Persistence;

/// <summary>
/// Persistiert Community-Bestandspositionen mit atomaren, parametrisierten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Add initialisiert ausschließlich im Add-Pfad eine fehlende Position mit Menge null und sperrt
/// sie anschließend mit <c>FOR UPDATE</c>. Remove legt fehlende Positionen nicht an. Sinkt eine
/// vorhandene Menge exakt auf null, wird die Zeile gelöscht, damit die Persistenz nur tatsächlich
/// vorhandene Bestände enthält. Jede Read/Modify/Write-Sequenz bleibt in derselben Transaktion.
/// </remarks>
public sealed class CommunityInventoryStore : ICommunityInventoryStore
{
    private const string InitializeSql = """
        INSERT INTO community_inventory_entries
            (community_identity_id, item_definition_id, quantity)
        VALUES
            (@CommunityIdentityId, @ItemDefinitionId, 0)
        ON CONFLICT (community_identity_id, item_definition_id) DO NOTHING;
        """;

    private const string SelectForUpdateSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            item_definition_id AS ItemDefinitionId,
            quantity AS Quantity
        FROM community_inventory_entries
        WHERE community_identity_id = @CommunityIdentityId
          AND item_definition_id = @ItemDefinitionId
        FOR UPDATE;
        """;

    private const string SelectSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            item_definition_id AS ItemDefinitionId,
            quantity AS Quantity
        FROM community_inventory_entries
        WHERE community_identity_id = @CommunityIdentityId
          AND item_definition_id = @ItemDefinitionId;
        """;

    private const string UpdateSql = """
        UPDATE community_inventory_entries
        SET quantity = @Quantity
        WHERE community_identity_id = @CommunityIdentityId
          AND item_definition_id = @ItemDefinitionId;
        """;

    private const string DeleteSql = """
        DELETE FROM community_inventory_entries
        WHERE community_identity_id = @CommunityIdentityId
          AND item_definition_id = @ItemDefinitionId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Store mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    public CommunityInventoryStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<InventoryQuantity> AddAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await AddInTransactionAsync(
                communityIdentityId,
                itemDefinitionId,
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
    public async Task<InventoryQuantity> RemoveAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await RemoveInTransactionAsync(
                communityIdentityId,
                itemDefinitionId,
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
    public async Task<CommunityInventoryEntry?> GetAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validItemDefinitionId = ItemDefinitionId.Create(itemDefinitionId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<InventoryRow>(
                new CommandDefinition(
                    SelectSql,
                    new
                    {
                        CommunityIdentityId = validCommunityIdentityId.Value,
                        ItemDefinitionId = validItemDefinitionId.Value
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    private static async Task<InventoryQuantity> AddInTransactionAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validItemDefinitionId = ItemDefinitionId.Create(itemDefinitionId.Value);

        var parameters = new
        {
            CommunityIdentityId = validCommunityIdentityId.Value,
            ItemDefinitionId = validItemDefinitionId.Value
        };

        await connection.ExecuteAsync(
                new CommandDefinition(
                    InitializeSql,
                    parameters,
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var row = await connection.QuerySingleAsync<InventoryRow>(
                new CommandDefinition(
                    SelectForUpdateSql,
                    parameters,
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var entry = ToDomain(row);
        entry.Add(amount);

        return await UpdateAsync(entry, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<InventoryQuantity> RemoveInTransactionAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validItemDefinitionId = ItemDefinitionId.Create(itemDefinitionId.Value);
        var parameters = new
        {
            CommunityIdentityId = validCommunityIdentityId.Value,
            ItemDefinitionId = validItemDefinitionId.Value
        };

        var row = await connection.QuerySingleOrDefaultAsync<InventoryRow>(
                new CommandDefinition(
                    SelectForUpdateSql,
                    parameters,
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var entry = row is null
            ? CommunityInventoryEntry.Rehydrate(
                validCommunityIdentityId,
                validItemDefinitionId,
                InventoryQuantity.Zero)
            : ToDomain(row);

        entry.Remove(amount);

        if (entry.Quantity == InventoryQuantity.Zero)
        {
            await DeleteAsync(entry, connection, transaction, cancellationToken).ConfigureAwait(false);
            return InventoryQuantity.Zero;
        }

        return await UpdateAsync(entry, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<InventoryQuantity> UpdateAsync(
        CommunityInventoryEntry entry,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var updatedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    UpdateSql,
                    new
                    {
                        CommunityIdentityId = entry.CommunityIdentityId.Value,
                        ItemDefinitionId = entry.ItemDefinitionId.Value,
                        Quantity = entry.Quantity.Value
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                "Die Community-Inventory-Bestandsposition konnte nicht eindeutig aktualisiert werden.");
        }

        return entry.Quantity;
    }

    private static async Task DeleteAsync(
        CommunityInventoryEntry entry,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var deletedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    DeleteSql,
                    new
                    {
                        CommunityIdentityId = entry.CommunityIdentityId.Value,
                        ItemDefinitionId = entry.ItemDefinitionId.Value
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (deletedRows != 1)
        {
            throw new InvalidOperationException(
                "Die Community-Inventory-Bestandsposition konnte nicht eindeutig gelöscht werden.");
        }
    }

    private static CommunityInventoryEntry ToDomain(InventoryRow row)
    {
        return CommunityInventoryEntry.Rehydrate(
            CommunityIdentityId.Create(row.CommunityIdentityId),
            ItemDefinitionId.Create(row.ItemDefinitionId),
            InventoryQuantity.Create(row.Quantity));
    }

    private sealed class InventoryRow
    {
        public Guid CommunityIdentityId { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public long Quantity { get; set; }
    }
}
