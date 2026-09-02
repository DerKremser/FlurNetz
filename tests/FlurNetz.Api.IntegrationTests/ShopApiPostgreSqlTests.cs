using System.Net;
using System.Net.Http.Json;
using System.Text;
using FlurNetz.Api.Contracts;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft die read-only Shop-HTTP-Grenze Ende zu Ende gegen den echten API-Host und PostgreSQL.
/// </summary>
public sealed class ShopApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task EmptyDatabaseStartupRunsAllRegisteredMigrations()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();

        using var factory = await StartHostAsync();
        await using var connection = await OpenConnectionAsync();
        await using var tableCommand = new NpgsqlCommand(
            """
            SELECT
                to_regclass('public.community_identities') IS NOT NULL
                AND to_regclass('public.community_economies') IS NOT NULL
                AND to_regclass('public.community_inventory_entries') IS NOT NULL
                AND to_regclass('public.shop_offers') IS NOT NULL
                AND to_regclass('public.shop_purchases') IS NOT NULL
                AND to_regclass('flurnetz_messaging.outbox_messages') IS NOT NULL
                AND to_regclass('flurnetz_messaging.inbox_messages') IS NOT NULL;
            """,
            connection);
        await using var historyCommand = new NpgsqlCommand(
            """
            SELECT owner, version, name
            FROM flurnetz_persistence.migration_history
            WHERE owner IN ('Identity', 'Economy', 'Inventory', 'Messaging', 'Shop')
            ORDER BY owner, version;
            """,
            connection);

        Assert.True((bool)(await tableCommand.ExecuteScalarAsync(TestToken))!);
        await using var reader = await historyCommand.ExecuteReaderAsync(TestToken);
        var history = new List<(string Owner, long Version, string Name)>();
        while (await reader.ReadAsync(TestToken))
        {
            history.Add((reader.GetString(0), reader.GetInt64(1), reader.GetString(2)));
        }

        Assert.Equal(
            new[]
            {
                ("Economy", 1L, "CreateCommunityEconomies"),
                ("Identity", 1L, "CreateCommunityIdentities"),
                ("Inventory", 1L, "CreateCommunityInventoryEntries"),
                ("Messaging", 1L, "CreateOutboxAndInbox"),
                ("Shop", 1L, "CreateShopOffers"),
                ("Shop", 2L, "CreateShopPurchases"),
                ("Shop", 3L, "AddShopOfferSortOrder")
            },
            history);
    }

    [Fact]
    public async Task ListOffersReturnsOnlyVisibleOffersAndMapsAllFields()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var now = CurrentUtc();
        var firstVisible = new OfferSeed(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.NewGuid(),
            "Sichtbar zuerst",
            "Beschreibung",
            42,
            true,
            now.AddHours(-1),
            now.AddHours(1),
            3,
            10);
        var secondVisible = new OfferSeed(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.NewGuid(),
            "Sichtbar gleich sortiert",
            null,
            43,
            true,
            null,
            null,
            null,
            10);
        var laterVisible = new OfferSeed(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.NewGuid(),
            "Sichtbar später",
            null,
            44,
            true,
            null,
            null,
            null,
            20);
        var disabled = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Deaktiviert",
            null,
            1,
            false,
            null,
            null,
            null);
        var future = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Zukünftig",
            null,
            2,
            true,
            now.AddHours(1),
            null,
            null);
        var expired = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Abgelaufen",
            null,
            3,
            true,
            null,
            now.AddHours(-1),
            null,
            0);
        await InsertOfferAsync(firstVisible);
        await InsertOfferAsync(secondVisible);
        await InsertOfferAsync(laterVisible);
        await InsertOfferAsync(disabled);
        await InsertOfferAsync(future);
        await InsertOfferAsync(expired);

        var response = await client.GetAsync("/api/shop/offers", TestToken);
        var body = await response.Content.ReadFromJsonAsync<ShopOfferListResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(
            new[] { firstVisible.Id, secondVisible.Id, laterVisible.Id },
            body!.Items.Select(item => item.Id).ToArray());
        var firstItem = body.Items[0];
        Assert.Equal(firstVisible.ItemDefinitionId, firstItem.ItemDefinitionId);
        Assert.Equal(firstVisible.DisplayName, firstItem.DisplayName);
        Assert.Equal(firstVisible.Description, firstItem.Description);
        Assert.Equal(firstVisible.Price, firstItem.Price);
        Assert.Equal(firstVisible.AvailableFromUtc, firstItem.AvailableFromUtc);
        Assert.Equal(firstVisible.AvailableUntilUtc, firstItem.AvailableUntilUtc);
        Assert.Equal(firstVisible.PurchaseLimitPerIdentity, firstItem.PurchaseLimitPerIdentity);
    }

    [Fact]
    public async Task VisibleOfferLookupReturnsDtoAndNonVisibleOffersReturnNotFound()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var now = CurrentUtc();
        var visible = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Sichtbares Einzelangebot",
            null,
            7,
            true,
            now.AddHours(-1),
            now.AddHours(1),
            null);
        var disabled = visible with { Id = Guid.NewGuid(), DisplayName = "Deaktiviert", IsEnabled = false };
        var future = visible with
        {
            Id = Guid.NewGuid(),
            DisplayName = "Zukünftig",
            AvailableFromUtc = now.AddHours(1),
            AvailableUntilUtc = null
        };
        var expired = visible with
        {
            Id = Guid.NewGuid(),
            DisplayName = "Abgelaufen",
            AvailableFromUtc = null,
            AvailableUntilUtc = now.AddHours(-1)
        };
        await InsertOfferAsync(visible);
        await InsertOfferAsync(disabled);
        await InsertOfferAsync(future);
        await InsertOfferAsync(expired);

        var visibleResponse = await client.GetAsync($"/api/shop/offers/{visible.Id}", TestToken);
        var visibleBody = await visibleResponse.Content.ReadFromJsonAsync<ShopOfferResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);
        Assert.NotNull(visibleBody);
        Assert.Equal(visible.Id, visibleBody!.Id);
        Assert.Equal(visible.ItemDefinitionId, visibleBody.ItemDefinitionId);
        Assert.Equal(visible.DisplayName, visibleBody.DisplayName);
        Assert.Equal(visible.Price, visibleBody.Price);

        foreach (var hiddenId in new[] { disabled.Id, future.Id, expired.Id, Guid.NewGuid() })
        {
            using var hiddenResponse = await client.GetAsync($"/api/shop/offers/{hiddenId}", TestToken);
            Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        }
    }

    [Fact]
    public async Task PurchaseLookupReturnsEverySnapshotFieldAndUnknownPurchaseIsNotFound()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Purchase-Angebot",
            null,
            11,
            true,
            null,
            null,
            null);
        await InsertOfferAsync(offer);
        var purchase = new PurchaseSeed(
            Guid.NewGuid(),
            offer.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            9,
            Utc(16, 15));
        await InsertPurchaseAsync(purchase);

        var response = await client.GetAsync($"/api/shop/purchases/{purchase.Id}", TestToken);
        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(purchase.Id, body!.Id);
        Assert.Equal(purchase.ShopOfferId, body.ShopOfferId);
        Assert.Equal(purchase.CommunityIdentityId, body.CommunityIdentityId);
        Assert.Equal(purchase.ItemDefinitionId, body.ItemDefinitionId);
        Assert.Equal(purchase.PricePaid, body.PricePaid);
        Assert.Equal(purchase.PurchasedAtUtc, body.PurchasedAtUtc);

        using var unknownResponse = await client.GetAsync(
            $"/api/shop/purchases/{Guid.NewGuid()}",
            TestToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task PurchaseHistoryIsIdentityIsolatedNewestFirstAndUsesIdDescendingForEqualTimes()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateAndInsertOfferAsync();
        var identity = Guid.NewGuid();
        var otherIdentity = Guid.NewGuid();
        var sameTime = Utc(12, 0);
        var lowerId = new PurchaseSeed(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            offer.Id,
            identity,
            Guid.NewGuid(),
            1,
            sameTime);
        var higherId = lowerId with
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            PricePaid = 3
        };
        var older = lowerId with
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            PricePaid = 2,
            PurchasedAtUtc = Utc(11, 0)
        };
        var other = lowerId with
        {
            Id = Guid.NewGuid(),
            CommunityIdentityId = otherIdentity,
            PurchasedAtUtc = Utc(13, 0)
        };
        await InsertPurchaseAsync(lowerId);
        await InsertPurchaseAsync(higherId);
        await InsertPurchaseAsync(older);
        await InsertPurchaseAsync(other);

        var response = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?pageSize=10",
            TestToken);
        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(
            new[] { higherId.Id, lowerId.Id, older.Id },
            body!.Items.Select(item => item.Id).ToArray());
        Assert.All(body.Items, item => Assert.Equal(identity, item.CommunityIdentityId));
        Assert.Null(body.NextCursor);
    }

    [Fact]
    public async Task PurchaseHistorySupportsPageSizeOneCursorRoundtripAndFinalPage()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateAndInsertOfferAsync();
        var identity = Guid.NewGuid();
        var purchases = new[]
        {
            new PurchaseSeed(Guid.NewGuid(), offer.Id, identity, Guid.NewGuid(), 1, Utc(15, 0)),
            new PurchaseSeed(Guid.NewGuid(), offer.Id, identity, Guid.NewGuid(), 2, Utc(14, 0)),
            new PurchaseSeed(Guid.NewGuid(), offer.Id, identity, Guid.NewGuid(), 3, Utc(13, 0))
        };
        foreach (var purchase in purchases)
        {
            await InsertPurchaseAsync(purchase);
        }

        var firstResponse = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?pageSize=1",
            TestToken);
        var first = await firstResponse.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(first);
        Assert.Equal(purchases[0].Id, Assert.Single(first!.Items).Id);
        Assert.NotNull(first.NextCursor);

        var secondResponse = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?pageSize=1&cursor={Uri.EscapeDataString(first.NextCursor!)}",
            TestToken);
        var second = await secondResponse.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(second);
        Assert.Equal(purchases[1].Id, Assert.Single(second!.Items).Id);
        Assert.NotNull(second.NextCursor);

        var lastResponse = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?pageSize=1&cursor={Uri.EscapeDataString(second.NextCursor!)}",
            TestToken);
        var last = await lastResponse.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, lastResponse.StatusCode);
        Assert.NotNull(last);
        Assert.Equal(purchases[2].Id, Assert.Single(last!.Items).Id);
        Assert.Null(last.NextCursor);
    }

    [Fact]
    public async Task PurchaseHistoryAcceptsPageSizeOneHundred()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();
        var offer = await CreateAndInsertOfferAsync();
        var identity = Guid.NewGuid();
        for (var index = 0; index < 2; index++)
        {
            await InsertPurchaseAsync(new PurchaseSeed(
                Guid.NewGuid(),
                offer.Id,
                identity,
                Guid.NewGuid(),
                index,
                Utc(12 + index, 0)));
        }

        using var response = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?pageSize=100",
            TestToken);
        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PurchaseHistoryRejectsPageSizesOutsideBounds(int pageSize)
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/shop/identities/{Guid.NewGuid()}/purchases?pageSize={pageSize}",
            TestToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PurchaseHistoryReturnsEmptyPageForEmptyAndUnknownIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/shop/identities/{Guid.NewGuid()}/purchases",
            TestToken);
        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body!.Items);
        Assert.Null(body.NextCursor);
    }

    [Fact]
    public async Task PurchaseHistoryRejectsMalformedBase64AndInvalidCursorPayloads()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();
        var identity = Guid.NewGuid();

        using var malformedResponse = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?cursor=not-base64!",
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);

        var invalidPayload = EncodeCursor("""{"version":2}""");
        using var invalidResponse = await client.GetAsync(
            $"/api/shop/identities/{identity}/purchases?cursor={Uri.EscapeDataString(invalidPayload)}",
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task PurchaseHistoryRejectsCursorForAnotherIdentity()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();
        var offer = await CreateAndInsertOfferAsync();
        var firstIdentity = Guid.NewGuid();
        var secondIdentity = Guid.NewGuid();
        await InsertPurchaseAsync(new PurchaseSeed(
            Guid.NewGuid(),
            offer.Id,
            firstIdentity,
            Guid.NewGuid(),
            1,
            Utc(12, 0)));
        await InsertPurchaseAsync(new PurchaseSeed(
            Guid.NewGuid(),
            offer.Id,
            firstIdentity,
            Guid.NewGuid(),
            2,
            Utc(11, 0)));

        var firstResponse = await client.GetAsync(
            $"/api/shop/identities/{firstIdentity}/purchases?pageSize=1",
            TestToken);
        var first = await firstResponse.Content.ReadFromJsonAsync<ShopPurchaseHistoryResponse>(TestToken);
        Assert.NotNull(first?.NextCursor);

        using var response = await client.GetAsync(
            $"/api/shop/identities/{secondIdentity}/purchases?cursor={Uri.EscapeDataString(first!.NextCursor!)}",
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRouteIdsReturnBadRequest()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var routes = new[]
        {
            "/api/shop/offers/not-a-guid",
            "/api/shop/offers/00000000-0000-0000-0000-000000000000",
            "/api/shop/purchases/not-a-guid",
            "/api/shop/identities/not-a-guid/purchases",
            "/api/shop/identities/00000000-0000-0000-0000-000000000000/purchases"
        };

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route, TestToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task PurchaseWriteRouteRejectsMissingBody()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/shop/offers/{Guid.NewGuid()}/purchases",
            content: null,
            TestToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<FlurNetzApiFactory> StartHostAsync()
    {
        var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var startupClient = factory.CreateClient();
        using var startupResponse = await startupClient.GetAsync("/api/shop/offers", TestToken);
        Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);
        return factory;
    }

    private async Task<OfferSeed> CreateAndInsertOfferAsync()
    {
        var offer = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test-Angebot",
            null,
            1,
            true,
            null,
            null,
            null);
        await InsertOfferAsync(offer);
        return offer;
    }

    private async Task InsertOfferAsync(OfferSeed offer)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO shop_offers
                (id, item_definition_id, display_name, description, price, is_enabled,
                 available_from, available_until, purchase_limit_per_identity, sort_order)
            VALUES
                (@id, @itemDefinitionId, @displayName, @description, @price, @isEnabled,
                 @availableFromUtc, @availableUntilUtc, @purchaseLimitPerIdentity, @sortOrder);
            """,
            connection);
        command.Parameters.AddWithValue("id", offer.Id);
        command.Parameters.AddWithValue("itemDefinitionId", offer.ItemDefinitionId);
        command.Parameters.AddWithValue("displayName", offer.DisplayName);
        command.Parameters.AddWithValue("description", (object?)offer.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("price", offer.Price);
        command.Parameters.AddWithValue("isEnabled", offer.IsEnabled);
        command.Parameters.AddWithValue("availableFromUtc", (object?)offer.AvailableFromUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("availableUntilUtc", (object?)offer.AvailableUntilUtc ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "purchaseLimitPerIdentity",
            (object?)offer.PurchaseLimitPerIdentity ?? DBNull.Value);
        command.Parameters.AddWithValue("sortOrder", offer.SortOrder);
        await command.ExecuteNonQueryAsync(TestToken);
    }

    private async Task InsertPurchaseAsync(PurchaseSeed purchase)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO shop_purchases
                (id, shop_offer_id, community_identity_id,
                 purchased_inventory_item_definition_id, price_paid, purchased_at)
            VALUES
                (@id, @shopOfferId, @communityIdentityId,
                 @itemDefinitionId, @pricePaid, @purchasedAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("id", purchase.Id);
        command.Parameters.AddWithValue("shopOfferId", purchase.ShopOfferId);
        command.Parameters.AddWithValue("communityIdentityId", purchase.CommunityIdentityId);
        command.Parameters.AddWithValue("itemDefinitionId", purchase.ItemDefinitionId);
        command.Parameters.AddWithValue("pricePaid", purchase.PricePaid);
        command.Parameters.AddWithValue("purchasedAtUtc", purchase.PurchasedAtUtc);
        await command.ExecuteNonQueryAsync(TestToken);
    }

    private async Task ResetDatabaseAsync()
    {
        await database.ResetDatabaseAsync(TestToken);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestToken);
        return connection;
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static string EncodeCursor(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static DateTimeOffset CurrentUtc()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(
            now.Ticks - (now.Ticks % TimeSpan.TicksPerMicrosecond),
            TimeSpan.Zero);
    }

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 8, 31, hour, minute, 0, TimeSpan.Zero);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed record OfferSeed(
        Guid Id,
        Guid ItemDefinitionId,
        string DisplayName,
        string? Description,
        long Price,
        bool IsEnabled,
        DateTimeOffset? AvailableFromUtc,
        DateTimeOffset? AvailableUntilUtc,
        int? PurchaseLimitPerIdentity,
        int SortOrder = 0);

    private sealed record PurchaseSeed(
        Guid Id,
        Guid ShopOfferId,
        Guid CommunityIdentityId,
        Guid ItemDefinitionId,
        long PricePaid,
        DateTimeOffset PurchasedAtUtc);
}
