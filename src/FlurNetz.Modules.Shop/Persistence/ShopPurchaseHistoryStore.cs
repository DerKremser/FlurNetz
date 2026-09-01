using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Persistence.Connections;

namespace FlurNetz.Modules.Shop.Persistence;

/// <summary>
/// Liest persistierte Shop-Käufe mit gezielten PostgreSQL-/Dapper-Queries.
/// </summary>
/// <remarks>
/// Jeder Read besteht aus genau einer normalen Abfrage ohne explizite Transaktion,
/// Sperre oder Snapshot über mehrere History-Seiten.
/// </remarks>
public sealed class ShopPurchaseHistoryStore : IShopPurchaseHistoryStore
{
    private const string GetSql = """
        SELECT
            id AS Id,
            shop_offer_id AS ShopOfferId,
            community_identity_id AS CommunityIdentityId,
            purchased_inventory_item_definition_id AS ItemDefinitionId,
            price_paid AS PricePaid,
            purchased_at AS PurchasedAt
        FROM shop_purchases
        WHERE id = @Id;
        """;

    private const string ListWithoutCursorSql = """
        SELECT
            id AS Id,
            shop_offer_id AS ShopOfferId,
            community_identity_id AS CommunityIdentityId,
            purchased_inventory_item_definition_id AS ItemDefinitionId,
            price_paid AS PricePaid,
            purchased_at AS PurchasedAt
        FROM shop_purchases
        WHERE community_identity_id = @CommunityIdentityId
        ORDER BY purchased_at DESC, id DESC
        LIMIT @Take;
        """;

    private const string ListAfterCursorSql = """
        SELECT
            id AS Id,
            shop_offer_id AS ShopOfferId,
            community_identity_id AS CommunityIdentityId,
            purchased_inventory_item_definition_id AS ItemDefinitionId,
            price_paid AS PricePaid,
            purchased_at AS PurchasedAt
        FROM shop_purchases
        WHERE community_identity_id = @CommunityIdentityId
          AND (
              purchased_at < @PurchasedAtUtc
              OR (purchased_at = @PurchasedAtUtc AND id < @ShopPurchaseId)
          )
        ORDER BY purchased_at DESC, id DESC
        LIMIT @Take;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Purchase-History-Store mit der vorhandenen Verbindungsfabrik.
    /// </summary>
    public ShopPurchaseHistoryStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<ShopPurchase?> GetAsync(
        ShopPurchaseId shopPurchaseId,
        CancellationToken cancellationToken = default)
    {
        var validShopPurchaseId = ShopPurchaseId.Create(shopPurchaseId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<ShopPurchaseRow>(
                new CommandDefinition(
                    GetSql,
                    new { Id = validShopPurchaseId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShopPurchase>> ListForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        ShopPurchaseHistoryCursor? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        if (take < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                "Die Anzahl der zu lesenden Käufe muss größer als null sein.");
        }

        if (cursor is not null && cursor.CommunityIdentityId != validCommunityIdentityId)
        {
            throw new ArgumentException(
                "Der History-Cursor gehört zu einer anderen Community-Identität.",
                nameof(cursor));
        }

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = cursor is null
            ? await connection.QueryAsync<ShopPurchaseRow>(
                    new CommandDefinition(
                        ListWithoutCursorSql,
                        new
                        {
                            CommunityIdentityId = validCommunityIdentityId.Value,
                            Take = take
                        },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false)
            : await connection.QueryAsync<ShopPurchaseRow>(
                    new CommandDefinition(
                        ListAfterCursorSql,
                        new
                        {
                            CommunityIdentityId = validCommunityIdentityId.Value,
                            PurchasedAtUtc = cursor.PurchasedAtUtc,
                            ShopPurchaseId = cursor.ShopPurchaseId.Value,
                            Take = take
                        },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    private static ShopPurchase Rehydrate(ShopPurchaseRow row) =>
        ShopPurchase.Rehydrate(
            ShopPurchaseId.Create(row.Id),
            ShopOfferId.Create(row.ShopOfferId),
            CommunityIdentityId.Create(row.CommunityIdentityId),
            ItemDefinitionId.Create(row.ItemDefinitionId),
            ShopPrice.Create(row.PricePaid),
            row.PurchasedAt);

    private sealed class ShopPurchaseRow
    {
        public Guid Id { get; set; }

        public Guid ShopOfferId { get; set; }

        public Guid CommunityIdentityId { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public long PricePaid { get; set; }

        public DateTimeOffset PurchasedAt { get; set; }
    }
}
