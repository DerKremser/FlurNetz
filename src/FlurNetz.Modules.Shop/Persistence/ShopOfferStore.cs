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
             available_from, available_until, purchase_limit_per_identity)
        VALUES
            (@Id, @ItemDefinitionId, @DisplayName, @Description, @Price, @IsEnabled,
             @AvailableFrom, @AvailableUntil, @PurchaseLimitPerIdentity);
        """;

    private const string GetSql = """
        SELECT
            id AS Id,
            item_definition_id AS ItemDefinitionId,
            display_name AS DisplayName,
            description AS Description,
            price AS Price,
            is_enabled AS IsEnabled,
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity
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
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity
        FROM shop_offers
        ORDER BY id;
        """;

    private const string GetForUpdateSql = """
        SELECT
            id AS Id,
            item_definition_id AS ItemDefinitionId,
            display_name AS DisplayName,
            description AS Description,
            price AS Price,
            is_enabled AS IsEnabled,
            available_from AS AvailableFrom,
            available_until AS AvailableUntil,
            purchase_limit_per_identity AS PurchaseLimitPerIdentity
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
            available_from = @AvailableFrom,
            available_until = @AvailableUntil,
            purchase_limit_per_identity = @PurchaseLimitPerIdentity
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
            var affectedRows = await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        AddSql,
                        ToParameters(offer),
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

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
    public async Task<TResult> ExecuteAsync<TResult>(
        ShopOfferId shopOfferId,
        Func<ShopOffer, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var row = await transaction.Connection.QuerySingleOrDefaultAsync<ShopOfferRow>(
                    new CommandDefinition(
                        GetForUpdateSql,
                        new { Id = validShopOfferId.Value },
                        transaction: transaction.Transaction,
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

            if (before != after)
            {
                var affectedRows = await transaction.Connection.ExecuteAsync(
                        new CommandDefinition(
                            UpdateSql,
                            new
                            {
                                Id = validShopOfferId.Value,
                                after.DisplayName,
                                after.Description,
                                Price = after.Price.Value,
                                after.IsEnabled,
                                AvailableFrom = ToUtc(after.AvailableFrom),
                                AvailableUntil = ToUtc(after.AvailableUntil),
                                after.PurchaseLimitPerIdentity
                            },
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                EnsureAffectedRows(affectedRows, "aktualisiert");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static object ToParameters(ShopOffer offer) => new
    {
        Id = offer.Id.Value,
        ItemDefinitionId = offer.ItemDefinitionId.Value,
        offer.DisplayName,
        offer.Description,
        Price = offer.Price.Value,
        offer.IsEnabled,
        AvailableFrom = ToUtc(offer.Availability.AvailableFrom),
        AvailableUntil = ToUtc(offer.Availability.AvailableUntil),
        offer.PurchaseLimitPerIdentity
    };

    private static DateTimeOffset? ToUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static ShopOffer Rehydrate(ShopOfferRow row)
    {
        return ShopOffer.Rehydrate(
            ShopOfferId.Create(row.Id),
            ItemDefinitionId.Create(row.ItemDefinitionId),
            row.DisplayName,
            row.Description,
            ShopPrice.Create(row.Price),
            row.IsEnabled,
            AvailabilityWindow.Create(row.AvailableFrom, row.AvailableUntil),
            row.PurchaseLimitPerIdentity);
    }

    private static ShopOfferSnapshot Snapshot(ShopOffer offer) =>
        new(
            offer.DisplayName,
            offer.Description,
            offer.Price,
            offer.IsEnabled,
            offer.Availability.AvailableFrom,
            offer.Availability.AvailableUntil,
            offer.PurchaseLimitPerIdentity);

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
        DateTimeOffset? AvailableFrom,
        DateTimeOffset? AvailableUntil,
        int? PurchaseLimitPerIdentity);

    private sealed class ShopOfferRow
    {
        public Guid Id { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long Price { get; set; }

        public bool IsEnabled { get; set; }

        public DateTimeOffset? AvailableFrom { get; set; }

        public DateTimeOffset? AvailableUntil { get; set; }

        public int? PurchaseLimitPerIdentity { get; set; }
    }
}
