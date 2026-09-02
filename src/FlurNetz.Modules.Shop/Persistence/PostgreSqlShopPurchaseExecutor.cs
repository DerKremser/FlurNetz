using Dapper;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Shop.Persistence;

/// <summary>
/// Führt einen Inventory-Shop-Kauf vollständig atomar innerhalb einer PostgreSQL-Transaktion aus.
/// </summary>
public sealed class PostgreSqlShopPurchaseExecutor : IShopPurchaseExecutor
{
    private const string ReserveRequestSql = """
        INSERT INTO shop_purchase_requests
            (request_id, shop_purchase_id, shop_offer_id, community_identity_id)
        VALUES
            (@RequestId, @ShopPurchaseId, @ShopOfferId, @CommunityIdentityId)
        ON CONFLICT (request_id) DO NOTHING
        RETURNING request_id;
        """;

    private const string LoadRequestSql = """
        SELECT
            request_id AS RequestId,
            shop_purchase_id AS ShopPurchaseId,
            shop_offer_id AS ShopOfferId,
            community_identity_id AS CommunityIdentityId
        FROM shop_purchase_requests
        WHERE request_id = @RequestId;
        """;

    private const string LoadPurchaseSql = """
        SELECT
            id AS Id,
            shop_offer_id AS ShopOfferId,
            community_identity_id AS CommunityIdentityId,
            purchased_inventory_item_definition_id AS ItemDefinitionId,
            price_paid AS PricePaid,
            purchased_at AS PurchasedAt
        FROM shop_purchases
        WHERE id = @ShopPurchaseId;
        """;

    private const string LoadOfferForShareSql = """
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
        WHERE id = @ShopOfferId
        FOR SHARE;
        """;

    private const string InitializePurchaseGuardSql = """
        INSERT INTO shop_purchase_guards
            (shop_offer_id, community_identity_id)
        VALUES
            (@ShopOfferId, @CommunityIdentityId)
        ON CONFLICT (shop_offer_id, community_identity_id) DO NOTHING;
        """;

    private const string LockPurchaseGuardSql = """
        SELECT 1
        FROM shop_purchase_guards
        WHERE shop_offer_id = @ShopOfferId
          AND community_identity_id = @CommunityIdentityId
        FOR UPDATE;
        """;

    private const string CountPurchasesSql = """
        SELECT COUNT(*)::bigint
        FROM shop_purchases
        WHERE shop_offer_id = @ShopOfferId
          AND community_identity_id = @CommunityIdentityId;
        """;

    private const string InsertPurchaseSql = """
        INSERT INTO shop_purchases
            (id, shop_offer_id, community_identity_id,
             purchased_inventory_item_definition_id, price_paid, purchased_at)
        VALUES
            (@Id, @ShopOfferId, @CommunityIdentityId,
             @ItemDefinitionId, @PricePaid, @PurchasedAt);
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly ICommunityIdentityExistence identityExistence;
    private readonly IEconomyBalanceDebit economyBalanceDebit;
    private readonly IInventoryQuantityGrant inventoryQuantityGrant;
    private readonly IIntegrationEventPublisher integrationEventPublisher;

    public PostgreSqlShopPurchaseExecutor(
        IPostgreSqlConnectionFactory connectionFactory,
        ICommunityIdentityExistence identityExistence,
        IEconomyBalanceDebit economyBalanceDebit,
        IInventoryQuantityGrant inventoryQuantityGrant,
        IIntegrationEventPublisher integrationEventPublisher)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(identityExistence);
        ArgumentNullException.ThrowIfNull(economyBalanceDebit);
        ArgumentNullException.ThrowIfNull(inventoryQuantityGrant);
        ArgumentNullException.ThrowIfNull(integrationEventPublisher);

        this.connectionFactory = connectionFactory;
        this.identityExistence = identityExistence;
        this.economyBalanceDebit = economyBalanceDebit;
        this.inventoryQuantityGrant = inventoryQuantityGrant;
        this.integrationEventPublisher = integrationEventPublisher;
    }

    /// <inheritdoc />
    public async Task<ShopPurchase> ExecuteAsync(
        ShopPurchaseRequestId requestId,
        ShopPurchaseId purchaseId,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        DateTimeOffset purchasedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var validRequestId = ShopPurchaseRequestId.Create(requestId.Value);
        var validPurchaseId = ShopPurchaseId.Create(purchaseId.Value);
        var validShopOfferId = ShopOfferId.Create(shopOfferId.Value);
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        EnsureValidPurchasedAtUtc(purchasedAtUtc);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var reservedRequestId = await transaction.Connection.QuerySingleOrDefaultAsync<Guid?>(
                    new CommandDefinition(
                        ReserveRequestSql,
                        new
                        {
                            RequestId = validRequestId.Value,
                            ShopPurchaseId = validPurchaseId.Value,
                            ShopOfferId = validShopOfferId.Value,
                            CommunityIdentityId = validCommunityIdentityId.Value
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (!reservedRequestId.HasValue)
            {
                var replay = await LoadReplayAsync(
                        validRequestId,
                        validShopOfferId,
                        validCommunityIdentityId,
                        transaction,
                        cancellationToken)
                    .ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return replay;
            }

            var identityExists = await identityExistence.ExistsAsync(
                    validCommunityIdentityId,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!identityExists)
            {
                throw new ShopPurchaseIdentityNotFoundException(validCommunityIdentityId);
            }

            var offerRow = await transaction.Connection.QuerySingleOrDefaultAsync<ShopOfferRow>(
                    new CommandDefinition(
                        LoadOfferForShareSql,
                        new { ShopOfferId = validShopOfferId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (offerRow is null)
            {
                throw new ShopOfferNotFoundException(validShopOfferId);
            }

            var offer = RehydrateOffer(offerRow);
            if (offer.IsArchived || !offer.IsEnabled || !offer.IsAvailableAt(purchasedAtUtc))
            {
                throw new ShopOfferUnavailableForPurchaseException(validShopOfferId);
            }

            if (offer.PurchaseLimitPerIdentity is int purchaseLimit)
            {
                var guardParameters = new
                {
                    ShopOfferId = validShopOfferId.Value,
                    CommunityIdentityId = validCommunityIdentityId.Value
                };

                await transaction.Connection.ExecuteAsync(
                        new CommandDefinition(
                            InitializePurchaseGuardSql,
                            guardParameters,
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                _ = await transaction.Connection.QuerySingleAsync<int>(
                        new CommandDefinition(
                            LockPurchaseGuardSql,
                            guardParameters,
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                var purchaseCount = await transaction.Connection.QuerySingleAsync<long>(
                        new CommandDefinition(
                            CountPurchasesSql,
                            guardParameters,
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                if (purchaseCount >= purchaseLimit)
                {
                    throw new ShopPurchaseLimitExceededException(
                        validShopOfferId,
                        validCommunityIdentityId,
                        purchaseLimit);
                }
            }

            if (offer.Price.Value > 0)
            {
                await economyBalanceDebit.DebitAsync(
                        validCommunityIdentityId,
                        offer.Price.Value,
                        transaction.Connection,
                        transaction.Transaction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await inventoryQuantityGrant.GrantAsync(
                    validCommunityIdentityId,
                    offer.ItemDefinitionId,
                    1,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            var purchase = ShopPurchase.Create(
                validPurchaseId,
                validShopOfferId,
                validCommunityIdentityId,
                offer.ItemDefinitionId,
                offer.Price,
                purchasedAtUtc);

            var insertedRows = await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        InsertPurchaseSql,
                        new
                        {
                            Id = purchase.Id.Value,
                            ShopOfferId = purchase.ShopOfferId.Value,
                            CommunityIdentityId = purchase.CommunityIdentityId.Value,
                            ItemDefinitionId = purchase.ItemDefinitionId.Value,
                            PricePaid = purchase.PricePaid.Value,
                            PurchasedAt = purchase.PurchasedAtUtc
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (insertedRows != 1)
            {
                throw new InvalidOperationException("Der Shop-Kauf konnte nicht eindeutig persistiert werden.");
            }

            var integrationEvent = new ShopPurchaseCompletedIntegrationEvent(
                purchase.Id.Value,
                purchase.ShopOfferId.Value,
                purchase.CommunityIdentityId.Value,
                purchase.ItemDefinitionId.Value,
                purchase.PricePaid.Value,
                purchase.PurchasedAtUtc);

            var envelope = new IntegrationEventEnvelope(
                Guid.NewGuid(),
                ShopPurchaseCompletedIntegrationEvent.MessageType,
                ShopPurchaseCompletedIntegrationEvent.SchemaVersion,
                purchase.PurchasedAtUtc,
                integrationEvent,
                validRequestId.Value.ToString("D"));

            await integrationEventPublisher.EnqueueAsync(
                    transaction,
                    envelope,
                    cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return purchase;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ShopPurchase> LoadReplayAsync(
        ShopPurchaseRequestId requestId,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        PostgreSqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var request = await transaction.Connection.QuerySingleOrDefaultAsync<PurchaseRequestRow>(
                new CommandDefinition(
                    LoadRequestSql,
                    new { RequestId = requestId.Value },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (request is null)
        {
            throw new InvalidOperationException(
                "Die idempotente Shop-Purchase-Reservation konnte nach einem Konflikt nicht geladen werden.");
        }

        if (request.ShopOfferId != shopOfferId.Value
            || request.CommunityIdentityId != communityIdentityId.Value)
        {
            throw new ShopPurchaseIdempotencyConflictException(requestId);
        }

        var purchaseRow = await transaction.Connection.QuerySingleOrDefaultAsync<ShopPurchaseRow>(
                new CommandDefinition(
                    LoadPurchaseSql,
                    new { request.ShopPurchaseId },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (purchaseRow is null)
        {
            throw new InvalidOperationException(
                "Die idempotente Shop-Purchase-Reservation verweist auf keinen persistierten Kauf.");
        }

        var purchase = RehydratePurchase(purchaseRow);
        if (purchase.ShopOfferId != shopOfferId
            || purchase.CommunityIdentityId != communityIdentityId)
        {
            throw new InvalidOperationException(
                "Persistierter Shop-Purchase und Idempotenz-Reservation sind inkonsistent.");
        }

        return purchase;
    }

    private static ShopOffer RehydrateOffer(ShopOfferRow row)
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

    private static ShopPurchase RehydratePurchase(ShopPurchaseRow row)
    {
        return ShopPurchase.Rehydrate(
            ShopPurchaseId.Create(row.Id),
            ShopOfferId.Create(row.ShopOfferId),
            CommunityIdentityId.Create(row.CommunityIdentityId),
            ItemDefinitionId.Create(row.ItemDefinitionId),
            ShopPrice.Create(row.PricePaid),
            row.PurchasedAt.ToUniversalTime());
    }

    private static void EnsureValidPurchasedAtUtc(DateTimeOffset purchasedAtUtc)
    {
        if (purchasedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Kaufzeitpunkt muss in UTC vorliegen.", nameof(purchasedAtUtc));
        }

        if (purchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Kaufzeitpunkt muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                nameof(purchasedAtUtc));
        }
    }

    private sealed class PurchaseRequestRow
    {
        public Guid RequestId { get; set; }

        public Guid ShopPurchaseId { get; set; }

        public Guid ShopOfferId { get; set; }

        public Guid CommunityIdentityId { get; set; }
    }

    private sealed class ShopPurchaseRow
    {
        public Guid Id { get; set; }

        public Guid ShopOfferId { get; set; }

        public Guid CommunityIdentityId { get; set; }

        public Guid ItemDefinitionId { get; set; }

        public long PricePaid { get; set; }

        public DateTimeOffset PurchasedAt { get; set; }
    }

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
