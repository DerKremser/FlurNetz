using System.Net;
using System.Net.Http.Json;
using System.Text;
using FlurNetz.Api.Contracts;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Processing;
using FlurNetz.Modules.Shop.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft den HTTP-Purchase-Adapter und seine echte Producer-Komposition gegen PostgreSQL.
/// </summary>
public sealed class ShopPurchaseApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task PaidPurchaseReturnsCompleteSnapshotAndPersistsAtomicEffects()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var itemDefinitionId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 100);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            itemDefinitionId,
            "Bezahltes Purchase-Angebot",
            42,
            true,
            null,
            null,
            3));

        using var response = await PurchaseAsync(client, offerId, requestId, identityId);
        var purchase = await ReadPurchaseAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(offerId, purchase.ShopOfferId);
        Assert.Equal(identityId, purchase.CommunityIdentityId);
        Assert.Equal(itemDefinitionId, purchase.ItemDefinitionId);
        Assert.Equal(42, purchase.PricePaid);
        Assert.NotEqual(default, purchase.Id);
        Assert.NotEqual(default, purchase.PurchasedAtUtc);
        Assert.Equal($"/api/shop/purchases/{purchase.Id}", response.Headers.Location?.OriginalString);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(58, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(1, await CountOutboxAsync(connection));

        var outbox = await ReadOutboxAsync(connection);
        Assert.NotNull(outbox);
        Assert.Equal(ShopPurchaseCompletedIntegrationEvent.MessageType, outbox!.MessageType);
        Assert.Equal(ShopPurchaseCompletedIntegrationEvent.SchemaVersion, outbox.SchemaVersion);
        Assert.Equal(requestId.ToString("D"), outbox.CorrelationId);
        Assert.Equal("pending", outbox.Status);
    }

    [Fact]
    public async Task FreePurchaseSucceedsWithoutEconomyRow()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var itemDefinitionId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            itemDefinitionId,
            "Kostenloses Purchase-Angebot",
            0,
            true,
            null,
            null,
            null));

        using var response = await PurchaseAsync(client, offerId, Guid.NewGuid(), identityId);
        var purchase = await ReadPurchaseAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, purchase.PricePaid);

        await using var connection = await OpenConnectionAsync();
        Assert.Null(await ReadBalanceOrNullAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task ReplayingTheSameRequestReturnsTheSamePurchaseAndAppliesEffectsOnce()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var itemDefinitionId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 10);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            itemDefinitionId,
            "Idempotentes Purchase-Angebot",
            4,
            true,
            null,
            null,
            null));

        using var firstResponse = await PurchaseAsync(client, offerId, requestId, identityId);
        var firstPurchase = await ReadPurchaseAsync(firstResponse);
        using var replayResponse = await PurchaseAsync(client, offerId, requestId, identityId);
        var replayPurchase = await ReadPurchaseAsync(replayResponse);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        Assert.Equal(firstPurchase, replayPurchase);
        Assert.Equal(
            $"/api/shop/purchases/{firstPurchase.Id}",
            replayResponse.Headers.Location?.OriginalString);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(6, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task ReusingRequestIdForAnotherOfferReturnsConflictWithoutSecondEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var firstItemDefinitionId = Guid.NewGuid();
        var secondItemDefinitionId = Guid.NewGuid();
        var firstOfferId = Guid.NewGuid();
        var secondOfferId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 20);
        await InsertOfferAsync(new OfferSeed(
            firstOfferId,
            firstItemDefinitionId,
            "Erstes Idempotenz-Angebot",
            4,
            true,
            null,
            null,
            null));
        await InsertOfferAsync(new OfferSeed(
            secondOfferId,
            secondItemDefinitionId,
            "Zweites Idempotenz-Angebot",
            7,
            true,
            null,
            null,
            null));

        using var firstResponse = await PurchaseAsync(client, firstOfferId, requestId, identityId);
        _ = await ReadPurchaseAsync(firstResponse);
        using var conflictResponse = await PurchaseAsync(client, secondOfferId, requestId, identityId);
        using var identityConflictResponse = await PurchaseAsync(
            client,
            firstOfferId,
            requestId,
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Conflict, identityConflictResponse.StatusCode);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(16, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, firstItemDefinitionId));
        Assert.Null(await ReadQuantityOrNullAsync(connection, identityId, secondItemDefinitionId));
        Assert.Equal(1, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task UnknownOfferReturnsNotFoundWithoutBusinessEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var itemDefinitionId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 20);

        using var response = await PurchaseAsync(client, Guid.NewGuid(), Guid.NewGuid(), identityId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(20, await ReadBalanceAsync(connection, identityId));
        Assert.Null(await ReadQuantityOrNullAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(0, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task UnknownIdentityReturnsNotFoundAndRollsBackTheCompletePurchase()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offerId = Guid.NewGuid();
        await InsertOfferAsync(new OfferSeed(
            offerId,
            Guid.NewGuid(),
            "Identity-Fehler-Angebot",
            5,
            true,
            null,
            null,
            1));

        using var response = await PurchaseAsync(client, offerId, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(0, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0, await CountAsync(connection, "community_economies"));
        Assert.Equal(0, await CountAsync(connection, "community_inventory_entries"));
        Assert.Equal(0, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task DisabledFutureAndExpiredOffersReturnConflict()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 30);
        var now = CurrentUtc();
        var offers = new[]
        {
            new OfferSeed(Guid.NewGuid(), Guid.NewGuid(), "Deaktiviertes Angebot", 5, false, null, null, null),
            new OfferSeed(Guid.NewGuid(), Guid.NewGuid(), "Zukünftiges Angebot", 5, true, now.AddHours(1), null, null),
            new OfferSeed(Guid.NewGuid(), Guid.NewGuid(), "Abgelaufenes Angebot", 5, true, null, now.AddHours(-1), null)
        };

        foreach (var offer in offers)
        {
            await InsertOfferAsync(offer);
            using var response = await PurchaseAsync(client, offer.Id, Guid.NewGuid(), identityId);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(30, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(0, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0, await CountAsync(connection, "community_inventory_entries"));
        Assert.Equal(0, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task ReachingThePurchaseLimitReturnsConflictWithoutAdditionalEffect()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var itemDefinitionId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 10);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            itemDefinitionId,
            "Limit-Angebot",
            3,
            true,
            null,
            null,
            1));

        using var firstResponse = await PurchaseAsync(client, offerId, Guid.NewGuid(), identityId);
        _ = await ReadPurchaseAsync(firstResponse);
        using var limitedResponse = await PurchaseAsync(client, offerId, Guid.NewGuid(), identityId);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, limitedResponse.StatusCode);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(7, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(1, await ReadQuantityAsync(connection, identityId, itemDefinitionId));
        Assert.Equal(1, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(1, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(1, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task InsufficientBalanceReturnsConflictAndRollsBackEveryPurchaseWrite()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 10);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            Guid.NewGuid(),
            "Zu teures Angebot",
            50,
            true,
            null,
            null,
            1));

        using var response = await PurchaseAsync(client, offerId, Guid.NewGuid(), identityId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var connection = await OpenConnectionAsync();
        Assert.Equal(10, await ReadBalanceAsync(connection, identityId));
        Assert.Equal(0, await CountAsync(connection, "shop_purchases"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_requests"));
        Assert.Equal(0, await CountAsync(connection, "shop_purchase_guards"));
        Assert.Equal(0, await CountAsync(connection, "community_inventory_entries"));
        Assert.Equal(0, await CountOutboxAsync(connection));
    }

    [Fact]
    public async Task InvalidRouteBodyAndEmptyIdentifiersReturnBadRequest()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var validOfferId = Guid.NewGuid();
        var validRequestId = Guid.NewGuid();
        var validIdentityId = Guid.NewGuid();
        var validRequest = new PurchaseShopOfferRequest(validRequestId, validIdentityId);

        using var malformedRouteResponse = await client.PostAsJsonAsync(
            "/api/shop/offers/not-a-guid/purchases",
            validRequest,
            TestToken);
        using var emptyOfferResponse = await client.PostAsJsonAsync(
            "/api/shop/offers/00000000-0000-0000-0000-000000000000/purchases",
            validRequest,
            TestToken);
        using var emptyRequestResponse = await client.PostAsJsonAsync(
            $"/api/shop/offers/{validOfferId}/purchases",
            new PurchaseShopOfferRequest(Guid.Empty, validIdentityId),
            TestToken);
        using var emptyIdentityResponse = await client.PostAsJsonAsync(
            $"/api/shop/offers/{validOfferId}/purchases",
            new PurchaseShopOfferRequest(validRequestId, Guid.Empty),
            TestToken);
        using var invalidJsonResponse = await client.PostAsync(
            $"/api/shop/offers/{validOfferId}/purchases",
            new StringContent("{", Encoding.UTF8, "application/json"),
            TestToken);

        Assert.Equal(HttpStatusCode.BadRequest, malformedRouteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyOfferResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyRequestResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyIdentityResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidJsonResponse.StatusCode);
    }

    [Fact]
    public async Task ApiIsProducerOnlyAndLeavesPurchaseEventPendingWithoutInboxEntry()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var identityId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertOfferAsync(new OfferSeed(
            offerId,
            Guid.NewGuid(),
            "Producer-Angebot",
            0,
            true,
            null,
            null,
            null));

        var requestId = Guid.NewGuid();
        using var response = await PurchaseAsync(client, offerId, requestId, identityId);
        _ = await ReadPurchaseAsync(response);

        var registry = factory.Services.GetRequiredService<IIntegrationEventTypeRegistry>();
        var descriptor = registry.Resolve(
            ShopPurchaseCompletedIntegrationEvent.MessageType,
            ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
        Assert.Equal(typeof(ShopPurchaseCompletedIntegrationEvent), descriptor.ClrType);
        Assert.IsType<PostgreSqlOutboxPublisher>(
            factory.Services.GetRequiredService<IIntegrationEventPublisher>());
        Assert.Null(factory.Services.GetService<OutboxProcessor>());
        Assert.Empty(factory.Services.GetServices<IIntegrationEventHandlerRegistration>());

        await using var connection = await OpenConnectionAsync();
        var outbox = await ReadOutboxAsync(connection);
        Assert.NotNull(outbox);
        Assert.Equal("pending", outbox!.Status);
        Assert.Equal(requestId.ToString("D"), outbox.CorrelationId);
        Assert.Equal(0, await CountInboxAsync(connection));
    }

    private async Task<FlurNetzApiFactory> StartHostAsync()
    {
        var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var startupClient = factory.CreateClient();
        using var startupResponse = await startupClient.GetAsync("/api/shop/offers", TestToken);
        Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);
        return factory;
    }

    private async Task<HttpResponseMessage> PurchaseAsync(
        HttpClient client,
        Guid offerId,
        Guid requestId,
        Guid identityId) => await client.PostAsJsonAsync(
        $"/api/shop/offers/{offerId}/purchases",
        new PurchaseShopOfferRequest(requestId, identityId),
        TestToken);

    private async Task InsertIdentityAsync(Guid identityId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO community_identities (id) VALUES (@id);",
            connection);
        command.Parameters.AddWithValue("id", identityId);
        await command.ExecuteNonQueryAsync(TestToken);
    }

    private async Task InsertEconomyAsync(Guid identityId, long balance)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO community_economies (community_identity_id, balance) VALUES (@id, @balance);",
            connection);
        command.Parameters.AddWithValue("id", identityId);
        command.Parameters.AddWithValue("balance", balance);
        await command.ExecuteNonQueryAsync(TestToken);
    }

    private async Task InsertOfferAsync(OfferSeed offer)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO shop_offers
                (id, item_definition_id, display_name, price, is_enabled,
                 available_from, available_until, purchase_limit_per_identity, sort_order)
            VALUES
                (@id, @itemDefinitionId, @displayName, @price, @isEnabled,
                 @availableFromUtc, @availableUntilUtc, @purchaseLimitPerIdentity, @sortOrder);
            """,
            connection);
        command.Parameters.AddWithValue("id", offer.Id);
        command.Parameters.AddWithValue("itemDefinitionId", offer.ItemDefinitionId);
        command.Parameters.AddWithValue("displayName", offer.DisplayName);
        command.Parameters.AddWithValue("price", offer.Price);
        command.Parameters.AddWithValue("isEnabled", offer.IsEnabled);
        command.Parameters.AddWithValue(
            "availableFromUtc",
            (object?)offer.AvailableFromUtc ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "availableUntilUtc",
            (object?)offer.AvailableUntilUtc ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "purchaseLimitPerIdentity",
            (object?)offer.PurchaseLimitPerIdentity ?? DBNull.Value);
        command.Parameters.AddWithValue("sortOrder", offer.SortOrder);
        await command.ExecuteNonQueryAsync(TestToken);
    }

    private async Task<long> ReadBalanceAsync(NpgsqlConnection connection, Guid identityId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT balance FROM community_economies WHERE community_identity_id = @id;",
            connection);
        command.Parameters.AddWithValue("id", identityId);
        return (long)(await command.ExecuteScalarAsync(TestToken))!;
    }

    private async Task<long?> ReadBalanceOrNullAsync(NpgsqlConnection connection, Guid identityId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT balance FROM community_economies WHERE community_identity_id = @id;",
            connection);
        command.Parameters.AddWithValue("id", identityId);
        var value = await command.ExecuteScalarAsync(TestToken);
        return value is null or DBNull ? null : (long)value;
    }

    private async Task<long> ReadQuantityAsync(
        NpgsqlConnection connection,
        Guid identityId,
        Guid itemDefinitionId)
    {
        var value = await ReadQuantityOrNullAsync(connection, identityId, itemDefinitionId);
        return value ?? throw new Xunit.Sdk.XunitException("Expected an inventory entry.");
    }

    private async Task<long?> ReadQuantityOrNullAsync(
        NpgsqlConnection connection,
        Guid identityId,
        Guid itemDefinitionId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT quantity
            FROM community_inventory_entries
            WHERE community_identity_id = @identityId
              AND item_definition_id = @itemDefinitionId;
            """,
            connection);
        command.Parameters.AddWithValue("identityId", identityId);
        command.Parameters.AddWithValue("itemDefinitionId", itemDefinitionId);
        var value = await command.ExecuteScalarAsync(TestToken);
        return value is null or DBNull ? null : (long)value;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string tableName)
    {
        var allowedTableNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "community_economies",
            "community_inventory_entries",
            "shop_purchases",
            "shop_purchase_requests",
            "shop_purchase_guards"
        };

        if (!allowedTableNames.Contains(tableName))
        {
            throw new ArgumentOutOfRangeException(nameof(tableName));
        }

        await using var command = new NpgsqlCommand(
            $"SELECT COUNT(*)::bigint FROM {tableName};",
            connection);
        return (long)(await command.ExecuteScalarAsync(TestToken))!;
    }

    private static async Task<long> CountOutboxAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*)::bigint FROM flurnetz_messaging.outbox_messages;",
            connection);
        return (long)(await command.ExecuteScalarAsync(TestToken))!;
    }

    private static async Task<long> CountInboxAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*)::bigint FROM flurnetz_messaging.inbox_messages;",
            connection);
        return (long)(await command.ExecuteScalarAsync(TestToken))!;
    }

    private static async Task<OutboxSnapshot?> ReadOutboxAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT message_type, schema_version, status, correlation_id
            FROM flurnetz_messaging.outbox_messages;
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(TestToken);
        if (!await reader.ReadAsync(TestToken))
        {
            return null;
        }

        return new OutboxSnapshot(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestToken);
        return connection;
    }

    private async Task ResetDatabaseAsync() => await database.ResetDatabaseAsync(TestToken);

    private static async Task<ShopPurchaseResponse> ReadPurchaseAsync(HttpResponseMessage response)
    {
        var purchase = await response.Content.ReadFromJsonAsync<ShopPurchaseResponse>(TestToken);
        return purchase ?? throw new Xunit.Sdk.XunitException("Expected a purchase response.");
    }

    private void SkipIfDatabaseIsUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static DateTimeOffset CurrentUtc()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(
            now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond,
            TimeSpan.Zero);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed record OfferSeed(
        Guid Id,
        Guid ItemDefinitionId,
        string DisplayName,
        long Price,
        bool IsEnabled,
        DateTimeOffset? AvailableFromUtc,
        DateTimeOffset? AvailableUntilUtc,
        int? PurchaseLimitPerIdentity,
        int SortOrder = 0);

    private sealed record OutboxSnapshot(
        string MessageType,
        int SchemaVersion,
        string Status,
        string? CorrelationId);
}
