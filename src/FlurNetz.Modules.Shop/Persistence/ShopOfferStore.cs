using Dapper;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Shop.Persistence;

/// <summary>
/// Persistiert Shop-Angebote mit gezielten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Katalogmutationen laden das Angebot mit <c>SELECT FOR UPDATE</c>. Dadurch werden
/// parallele Änderungen desselben Angebots serialisiert, während unterschiedliche Angebote
/// unabhängig voneinander mutiert werden können.
/// </remarks>
public sealed class ShopOfferStore : IShopOfferStore
{
    private const string AddSql = """
        INSERT INTO shop_offers
            (id, item_definition_id, display_name, description, price, is_enabled,
             is_archived, available_from, available_until, purchase_limit_per_identity, sort_order)
        VALUES
            (@Id, @ItemDefinitionId, @DisplayName, @Description, @Price, @IsEnabled,
             @IsArchived, @AvailableFrom, @AvailableUntil, @PurchaseLimitPerIdentity, @SortOrder);
        """;

    private const string GetSql = """
        SELECT
            id AS Id,
            item_definition_id AS ItemDefinitionId,
            display_name AS DisplayName,
            description AS Description,
            price AS Price,
            is_enabled AS IsEnabled,
            is_archived AS IsArchived,
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity,
            sort_order AS SortOrder
        FROM shop_offers
        WHERE id = @Id;
        """;

    private const string ListSql = """
        SELECT
            id AS Id,
            item_definition_id AS ItemDefinitionId,
            display_name AS DisplayName,
            description AS Description,
            price AS Price,
            is_enabled AS IsEnabled,
            is_archived AS IsArchived,
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity,
            sort_order AS SortOrder
        FROM shop_offers
        ORDER BY sort_order ASC, id ASC;
        """;

    private const string GetForUpdateSql = """
        SELECT
            id AS Id,
            item_definition_id AS ItemDefinitionId,
            display_name AS DisplayName,
            description AS Description,
            price AS Price,
            is_enabled AS IsEnabled,
            is_archived AS IsArchived,
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity,
            sort_order AS SortOrder
        FROM shop_offers
        WHERE id = @Id
        FOR UPDATE;
        """;

    private const string UpdateSql = """
        UPDATE shop_offers
        SET
            display_name = @DisplayName,
            description = @Description,
            price = @Price,
            is_enabled = @IsEnabled,
            is_archived = @IsArchived,
            available_from = @AvailableFrom,
            available_until = @AvailableUntil,
            purchase_limit_per_identity = @PurchaseLimitPerIdentity,
            sort_order = @SortOrder
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Katalog-Store mit der technischen Verbindungsfabrik.
    /// </summary>
    public ShopOfferStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        ShopOffer offer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var affectedRows = await AddAsync(offer, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            EnsureAffectedRows(affectedRows, "eingefügt");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ShopOffer?> GetAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default)
    {
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<ShopOfferRow>(
                new CommandDefinition(
                    GetSql,
                    new { Id = validShopOfferId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShopOffer>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<ShopOfferRow>(
                new CommandDefinition(
                    ListSql,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        Func<ShopOffer, bool> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await ExecuteAsync(
                    validShopOfferId,
                    operation,
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

    public async Task<int> AddAsync(
        ShopOffer offer,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return await connection.ExecuteAsync(new CommandDefinition(
            AddSql, ToParameters(offer), transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        Func<ShopOffer, bool> operation,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);
        var row = await connection.QuerySingleOrDefaultAsync<ShopOfferRow>(
                new CommandDefinition(
                    GetForUpdateSql,
                    new { Id = validShopOfferId.Value },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new ShopOfferNotFoundException(validShopOfferId);
        }

        var offer = Rehydrate(row);
        var before = Snapshot(offer);
        var result = operation(offer);
        var after = Snapshot(offer);

        if (before == after)
        {
            return result;
        }

        var affectedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    UpdateSql,
                    new
                    {
                        Id = validShopOfferId.Value,
                        after.DisplayName,
                        after.Description,
                        Price = after.Price.Value,
                        after.IsEnabled,
                        after.IsArchived,
                        AvailableFrom = after.AvailableFrom,
                        AvailableUntil = after.AvailableUntil,
                        after.PurchaseLimitPerIdentity,
                        after.SortOrder
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        EnsureAffectedRows(affectedRows, "aktualisiert");
        return result;
    }

    private static object ToParameters(ShopOffer offer) => new
    {
        Id = offer.Id.Value,
        ItemDefinitionId = offer.ItemDefinitionId.Value,
        offer.DisplayName,
        offer.Description,
        Price = offer.Price.Value,
        offer.IsEnabled,
        offer.IsArchived,
        AvailableFrom = offer.Availability.AvailableFrom,
        AvailableUntil = offer.Availability.AvailableUntil,
        offer.PurchaseLimitPerIdentity,
        offer.SortOrder
    };

    private static ShopOffer Rehydrate(ShopOfferRow row)
    {
        return ShopOffer.Rehydrate(
            ShopOfferId.Create(row.Id),
            ItemDefinitionId.Create(row.ItemDefinitionId),
            row.DisplayName,
            row.Description,
            ShopPrice.Create(row.Price),
            row.IsEnabled,
            row.IsArchived,
            AvailabilityWindow.Create(row.AvailableFrom, row.AvailableUntil),
            row.PurchaseLimitPerIdentity,
            row.SortOrder);
    }

    private static ShopOfferSnapshot Snapshot(ShopOffer offer) =>
        new(
            offer.DisplayName,
            offer.Description,
            offer.Price,
            offer.IsEnabled,
            offer.IsArchived,
            offer.Availability.AvailableFrom,
            offer.Availability.AvailableUntil,
            offer.PurchaseLimitPerIdentity,
            offer.SortOrder);

    private static void EnsureAffectedRows(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Das Shop-Angebot konnte nicht eindeutig {operation} werden.");
        }
    }

    private readonly record struct ShopOfferSnapshot(
        string DisplayName,
        string? Description,
        ShopPrice Price,
        bool IsEnabled,
        bool IsArchived,
        DateTimeOffset? AvailableFrom,
        DateTimeOffset? AvailableUntil,
        int? PurchaseLimitPerIdentity,
        int SortOrder);

    private sealed class ShopOfferRow
    {
        public Guid Id { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long Price { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsArchived { get; set; }

        public DateTimeOffset? AvailableFrom { get; set; }

        public DateTimeOffset? AvailableUntil { get; set; }

        public int? PurchaseLimitPerIdentity { get; set; }

        public int SortOrder { get; set; }
    }
}
