using System.Net;
using System.Net.Http.Json;
using System.Text;
using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using Npgsql;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft die getrennte Shop-Katalogverwaltung über den echten API-Host und PostgreSQL.
/// </summary>
public sealed class ShopManagementApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task CreatePersistsAllFieldsWithServerGeneratedDisabledOfferId()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var itemDefinitionId = Guid.NewGuid();
        var availableFrom = UtcNow().AddHours(1);
        var availableUntil = availableFrom.AddHours(2);
        var request = new CreateShopOfferRequest(
            itemDefinitionId,
            "  Management-Angebot  ",
            "  Beschreibung  ",
            42,
            availableFrom,
            availableUntil,
            3,
            17);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/shop/offers",
            request,
            TestToken);
        var body = await ReadManagementOfferAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(
            $"/api/admin/shop/offers/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(itemDefinitionId, body.ItemDefinitionId);
        Assert.Equal("Management-Angebot", body.DisplayName);
        Assert.Equal("Beschreibung", body.Description);
        Assert.Equal(42, body.Price);
        Assert.False(body.IsEnabled);
        Assert.Equal(availableFrom, body.AvailableFromUtc);
        Assert.Equal(availableUntil, body.AvailableUntilUtc);
        Assert.Equal(3, body.PurchaseLimitPerIdentity);
        Assert.Equal(17, body.SortOrder);

        var persisted = await ReadPersistedOfferAsync(body.Id);
        Assert.NotNull(persisted);
        Assert.Equal(body.Id, persisted!.Id);
        Assert.Equal(body.ItemDefinitionId, persisted.ItemDefinitionId);
        Assert.Equal(body.DisplayName, persisted.DisplayName);
        Assert.Equal(body.Description, persisted.Description);
        Assert.Equal(body.Price, persisted.Price);
        Assert.False(persisted.IsEnabled);
        Assert.Equal(body.AvailableFromUtc, persisted.AvailableFromUtc);
        Assert.Equal(body.AvailableUntilUtc, persisted.AvailableUntilUtc);
        Assert.Equal(body.PurchaseLimitPerIdentity, persisted.PurchaseLimitPerIdentity);
        Assert.Equal(body.SortOrder, persisted.SortOrder);
    }

    [Fact]
    public async Task CreateWithoutOptionalDescriptionWorksAndManagementListIncludesNonVisibleOffers()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var createdRequest = new CreateShopOfferRequest(
            Guid.NewGuid(),
            "Ohne Beschreibung",
            null,
            0,
            null,
            null,
            null);
        using var createdResponse = await client.PostAsJsonAsync(
            "/api/admin/shop/offers",
            createdRequest,
            TestToken);
        var created = await ReadManagementOfferAsync(createdResponse);

        var now = UtcNow();
        var future = new OfferSeed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Zukünftig",
            "Future",
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
            null);
        await InsertOfferAsync(future);
        await InsertOfferAsync(expired);

        using var listResponse = await client.GetAsync(
            "/api/admin/shop/offers",
            TestToken);
        var list = await listResponse.Content
            .ReadFromJsonAsync<ShopOfferManagementListResponse>(TestToken);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Null(created.Description);
        Assert.Equal(0, created.SortOrder);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        Assert.Equal(
            new[] { created.Id, future.Id, expired.Id }.OrderBy(id => id),
            list!.Items.Select(item => item.Id).OrderBy(id => id));
        Assert.Contains(list.Items, item => item.Id == created.Id && !item.IsEnabled);
        Assert.Contains(list.Items, item => item.Id == future.Id && item.IsEnabled);
        Assert.Contains(list.Items, item => item.Id == expired.Id && item.IsEnabled);
    }

    [Fact]
    public async Task ManagementGetReturnsDisabledOfferAndUnknownOrInvalidIdsReturnExpectedStatus()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offerId = Guid.NewGuid();
        await InsertOfferAsync(new OfferSeed(
            offerId,
            Guid.NewGuid(),
            "Deaktiviertes Angebot",
            null,
            5,
            false,
            null,
            null,
            null));

        using var getResponse = await client.GetAsync(
            $"/api/admin/shop/offers/{offerId}",
            TestToken);
        var body = await ReadManagementOfferAsync(getResponse);
        using var unknownResponse = await client.GetAsync(
            $"/api/admin/shop/offers/{Guid.NewGuid()}",
            TestToken);
        using var invalidResponse = await client.GetAsync(
            "/api/admin/shop/offers/not-a-guid",
            TestToken);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(offerId, body.Id);
        Assert.False(body.IsEnabled);
        Assert.Equal(0, body.SortOrder);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal(
            "application/problem+json",
            invalidResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ManagementListUsesSortOrderThenOfferIdAndSortOrderMutationIsPersisted()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var first = await CreateOfferAsync(client, sortOrder: 20);
        var second = await CreateOfferAsync(client, sortOrder: 0);
        var third = await CreateOfferAsync(client, sortOrder: 0);

        using var initialListResponse = await client.GetAsync(
            "/api/admin/shop/offers",
            TestToken);
        var initialList = await initialListResponse.Content
            .ReadFromJsonAsync<ShopOfferManagementListResponse>(TestToken);

        Assert.Equal(HttpStatusCode.OK, initialListResponse.StatusCode);
        Assert.NotNull(initialList);
        Assert.Equal(
            new[] { second, third, first }
                .OrderBy(offer => offer.SortOrder)
                .ThenBy(offer => offer.Id)
                .Select(offer => offer.Id),
            initialList!.Items.Select(offer => offer.Id));
        Assert.All(initialList.Items, offer =>
            Assert.Equal(
                offer.Id == first.Id ? 20 : 0,
                offer.SortOrder));

        using var changeResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{first.Id}/sort-order",
            new ChangeShopOfferSortOrderRequest(0));
        using var repeatedChangeResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{first.Id}/sort-order",
            new ChangeShopOfferSortOrderRequest(0));

        using var finalListResponse = await client.GetAsync(
            "/api/admin/shop/offers",
            TestToken);
        var finalList = await finalListResponse.Content
            .ReadFromJsonAsync<ShopOfferManagementListResponse>(TestToken);

        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedChangeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, finalListResponse.StatusCode);
        Assert.NotNull(finalList);
        Assert.Equal(
            new[] { first, second, third }
                .OrderBy(offer => offer.Id)
                .Select(offer => offer.Id),
            finalList!.Items.Select(offer => offer.Id));
        Assert.Equal(0, (await ReadPersistedOfferAsync(first.Id))!.SortOrder);
    }

    [Fact]
    public async Task AllManagementMutationsPersistAndRepeatedEnableDisableRemainNoOps()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateOfferAsync(client);
        var availableFrom = UtcNow().AddDays(1);
        var availableUntil = availableFrom.AddDays(1);

        using var renameResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/display-name",
            new RenameShopOfferRequest("Umbenannt"));
        using var setDescriptionResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/description",
            new ChangeShopOfferDescriptionRequest("Neue Beschreibung"));
        using var removeDescriptionResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/description",
            new ChangeShopOfferDescriptionRequest(null));
        using var priceResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/price",
            new ChangeShopOfferPriceRequest(77));
        using var availabilityResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/availability",
            new ChangeShopOfferAvailabilityRequest(availableFrom, availableUntil));
        using var limitResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/purchase-limit",
            new ChangeShopOfferPurchaseLimitRequest(4));
        using var removeLimitResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/purchase-limit",
            new ChangeShopOfferPurchaseLimitRequest(null));
        using var sortOrderResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/sort-order",
            new ChangeShopOfferSortOrderRequest(12));
        using var repeatedSortOrderResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/sort-order",
            new ChangeShopOfferSortOrderRequest(12));
        using var firstEnableResponse = await client.PostAsync(
            $"/api/admin/shop/offers/{offer.Id}/enable",
            content: null,
            TestToken);
        using var repeatedEnableResponse = await client.PostAsync(
            $"/api/admin/shop/offers/{offer.Id}/enable",
            content: null,
            TestToken);
        using var firstDisableResponse = await client.PostAsync(
            $"/api/admin/shop/offers/{offer.Id}/disable",
            content: null,
            TestToken);
        using var repeatedDisableResponse = await client.PostAsync(
            $"/api/admin/shop/offers/{offer.Id}/disable",
            content: null,
            TestToken);

        using var getResponse = await client.GetAsync(
            $"/api/admin/shop/offers/{offer.Id}",
            TestToken);
        var updated = await ReadManagementOfferAsync(getResponse);

        Assert.All(
            new[]
            {
                renameResponse,
                setDescriptionResponse,
                removeDescriptionResponse,
                priceResponse,
                availabilityResponse,
                limitResponse,
                removeLimitResponse,
                sortOrderResponse,
                repeatedSortOrderResponse,
                firstEnableResponse,
                repeatedEnableResponse,
                firstDisableResponse,
                repeatedDisableResponse
            },
            response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.Equal("Umbenannt", updated.DisplayName);
        Assert.Null(updated.Description);
        Assert.Equal(77, updated.Price);
        Assert.Equal(availableFrom, updated.AvailableFromUtc);
        Assert.Equal(availableUntil, updated.AvailableUntilUtc);
        Assert.Null(updated.PurchaseLimitPerIdentity);
        Assert.Equal(12, updated.SortOrder);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task InvalidManagementValuesAndMalformedJsonReturnProblemDetails400()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateOfferAsync(client);
        var invalidWindowBoundary = UtcNow();
        var invalidRequests = new (string Route, object Body)[]
        {
            ("/api/admin/shop/offers", new CreateShopOfferRequest(
                Guid.Empty, "Gültig", null, 1, null, null, null)),
            ("/api/admin/shop/offers", new CreateShopOfferRequest(
                Guid.NewGuid(), "   ", null, 1, null, null, null)),
            ("/api/admin/shop/offers", new CreateShopOfferRequest(
                Guid.NewGuid(), "Gültig", null, -1, null, null, null)),
            ("/api/admin/shop/offers", new CreateShopOfferRequest(
                Guid.NewGuid(), "Gültig", null, null, null, null, null)),
            ("/api/admin/shop/offers", new CreateShopOfferRequest(
                Guid.NewGuid(), "Gültig", null, 1, invalidWindowBoundary, invalidWindowBoundary, null)),
            ($"/api/admin/shop/offers/{offer.Id}/price", new ChangeShopOfferPriceRequest(-1)),
            ($"/api/admin/shop/offers/{offer.Id}/price", new ChangeShopOfferPriceRequest(null)),
            ($"/api/admin/shop/offers/{offer.Id}/availability", new ChangeShopOfferAvailabilityRequest(
                invalidWindowBoundary, invalidWindowBoundary)),
            ($"/api/admin/shop/offers/{offer.Id}/purchase-limit", new ChangeShopOfferPurchaseLimitRequest(0)),
            ($"/api/admin/shop/offers/{offer.Id}/sort-order", new ChangeShopOfferSortOrderRequest(-1)),
            ($"/api/admin/shop/offers/{offer.Id}/display-name", new RenameShopOfferRequest(" "))
        };

        foreach (var invalidRequest in invalidRequests)
        {
            using var response = invalidRequest.Route.EndsWith("/offers", StringComparison.Ordinal)
                ? await client.PostAsJsonAsync(invalidRequest.Route, invalidRequest.Body, TestToken)
                : await PutAsync(client, invalidRequest.Route, invalidRequest.Body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        using var malformedResponse = await client.PutAsync(
            $"/api/admin/shop/offers/{offer.Id}/price",
            new StringContent("{", Encoding.UTF8, "application/json"),
            TestToken);
        using var missingBodyResponse = await client.PutAsync(
            $"/api/admin/shop/offers/{offer.Id}/display-name",
            content: null,
            TestToken);
        using var missingSortOrderResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/sort-order",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingBodyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingSortOrderResponse.StatusCode);
    }

    [Fact]
    public async Task MutatingUnknownOfferAndInvalidMutationRouteReturn404And400()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();
        var unknownOfferId = Guid.NewGuid();
        var routes = new (string Route, object Body)[]
        {
            ($"/api/admin/shop/offers/{unknownOfferId}/display-name", new RenameShopOfferRequest("Name")),
            ($"/api/admin/shop/offers/{unknownOfferId}/description", new ChangeShopOfferDescriptionRequest("Text")),
            ($"/api/admin/shop/offers/{unknownOfferId}/price", new ChangeShopOfferPriceRequest(1)),
            ($"/api/admin/shop/offers/{unknownOfferId}/availability", new ChangeShopOfferAvailabilityRequest(null, null)),
            ($"/api/admin/shop/offers/{unknownOfferId}/purchase-limit", new ChangeShopOfferPurchaseLimitRequest(null)),
            ($"/api/admin/shop/offers/{unknownOfferId}/sort-order", new ChangeShopOfferSortOrderRequest(1)),
            ($"/api/admin/shop/offers/{unknownOfferId}/enable", new { }),
            ($"/api/admin/shop/offers/{unknownOfferId}/disable", new { })
        };

        foreach (var route in routes)
        {
            using var response = route.Route.EndsWith("/enable", StringComparison.Ordinal)
                || route.Route.EndsWith("/disable", StringComparison.Ordinal)
                ? await client.PostAsJsonAsync(route.Route, route.Body, TestToken)
                : await PutAsync(client, route.Route, route.Body);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var invalidMutationRouteResponse = await client.PostAsync(
            "/api/admin/shop/offers/not-a-guid/enable",
            content: null,
            TestToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidMutationRouteResponse.StatusCode);
    }

    [Fact]
    public async Task StorefrontRemainsHiddenUntilOfferIsEnabledAndCurrentlyAvailable()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();
        var offer = await CreateOfferAsync(client);

        using var initiallyHidden = await client.GetAsync(
            $"/api/shop/offers/{offer.Id}",
            TestToken);
        Assert.Equal(HttpStatusCode.NotFound, initiallyHidden.StatusCode);

        using var enableResponse = await client.PostAsync(
            $"/api/admin/shop/offers/{offer.Id}/enable",
            content: null,
            TestToken);
        using var visibleResponse = await client.GetAsync(
            $"/api/shop/offers/{offer.Id}",
            TestToken);
        Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);

        var futureFrom = UtcNow().AddHours(1);
        using var futureResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/availability",
            new ChangeShopOfferAvailabilityRequest(futureFrom, null));
        using var futureStorefrontResponse = await client.GetAsync(
            $"/api/shop/offers/{offer.Id}",
            TestToken);

        Assert.Equal(HttpStatusCode.NoContent, futureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, futureStorefrontResponse.StatusCode);
    }

    [Fact]
    public async Task PriceMutationIsUsedByLaterPurchaseButHistoricalSnapshotStaysUnchanged()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateOfferAsync(client, price: 3);
        await EnableAsync(client, offer.Id);
        var identityId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        await InsertEconomyAsync(identityId, 20);

        using var firstPurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);
        var firstPurchase = await firstPurchaseResponse.Content
            .ReadFromJsonAsync<ShopPurchaseResponse>(TestToken);

        using var priceResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/price",
            new ChangeShopOfferPriceRequest(8));
        using var secondPurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);
        var secondPurchase = await secondPurchaseResponse.Content
            .ReadFromJsonAsync<ShopPurchaseResponse>(TestToken);

        Assert.Equal(HttpStatusCode.Created, firstPurchaseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, priceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondPurchaseResponse.StatusCode);
        Assert.NotNull(firstPurchase);
        Assert.NotNull(secondPurchase);
        Assert.Equal(3, firstPurchase!.PricePaid);
        Assert.Equal(8, secondPurchase!.PricePaid);

        using var firstLookupResponse = await client.GetAsync(
            $"/api/shop/purchases/{firstPurchase.Id}",
            TestToken);
        var firstLookup = await firstLookupResponse.Content
            .ReadFromJsonAsync<ShopPurchaseResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, firstLookupResponse.StatusCode);
        Assert.Equal(3, firstLookup!.PricePaid);
    }

    [Fact]
    public async Task AvailabilityMutationControlsStorefrontAndPurchase()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateOfferAsync(client, price: 0);
        await EnableAsync(client, offer.Id);
        var identityId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);

        using var futureResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/availability",
            new ChangeShopOfferAvailabilityRequest(UtcNow().AddHours(1), null));
        using var futurePurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);

        using var availableResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/availability",
            new ChangeShopOfferAvailabilityRequest(null, null));
        using var availableStorefrontResponse = await client.GetAsync(
            $"/api/shop/offers/{offer.Id}",
            TestToken);
        using var availablePurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);

        Assert.Equal(HttpStatusCode.NoContent, futureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, futurePurchaseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, availableResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, availableStorefrontResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, availablePurchaseResponse.StatusCode);
    }

    [Fact]
    public async Task PurchaseLimitMutationControlsLaterPurchasesAndCanBeRemoved()
    {
        SkipIfDatabaseIsUnavailable();
        await ResetDatabaseAsync();
        using var factory = await StartHostAsync();
        using var client = factory.CreateClient();

        var offer = await CreateOfferAsync(client, price: 0);
        await EnableAsync(client, offer.Id);
        var identityId = Guid.NewGuid();
        await InsertIdentityAsync(identityId);
        using var setLimitResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/purchase-limit",
            new ChangeShopOfferPurchaseLimitRequest(1));
        using var firstPurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);
        using var limitedPurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);
        using var removeLimitResponse = await PutAsync(
            client,
            $"/api/admin/shop/offers/{offer.Id}/purchase-limit",
            new ChangeShopOfferPurchaseLimitRequest(null));
        using var laterPurchaseResponse = await PurchaseAsync(
            client,
            offer.Id,
            Guid.NewGuid(),
            identityId);

        Assert.Equal(HttpStatusCode.NoContent, setLimitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, firstPurchaseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, limitedPurchaseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeLimitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, laterPurchaseResponse.StatusCode);
    }

    private async Task<ShopOfferManagementResponse> CreateOfferAsync(
        HttpClient client,
        long price = 1,
        int sortOrder = 0)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/shop/offers",
            new CreateShopOfferRequest(
                Guid.NewGuid(),
                "Test-Angebot",
                null,
                price,
                null,
                null,
                null,
                sortOrder),
            TestToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadManagementOfferAsync(response);
    }

    private static async Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        string route,
        object body) => await client.PutAsJsonAsync(route, body, TestToken);

    private static async Task<HttpResponseMessage> PurchaseAsync(
        HttpClient client,
        Guid offerId,
        Guid requestId,
        Guid identityId) => await client.PostAsJsonAsync(
        $"/api/shop/offers/{offerId}/purchases",
        new PurchaseShopOfferRequest(requestId, identityId),
        TestToken);

    private async Task EnableAsync(HttpClient client, Guid offerId)
    {
        using var response = await client.PostAsync(
            $"/api/admin/shop/offers/{offerId}/enable",
            content: null,
            TestToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<FlurNetzApiFactory> StartHostAsync()
    {
        var factory = new FlurNetzApiFactory(database.ConnectionString);
        using var startupClient = factory.CreateClient();
        using var startupResponse = await startupClient.GetAsync(
            "/api/admin/shop/offers",
            TestToken);
        Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);
        return factory;
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

    private async Task<PersistedOffer?> ReadPersistedOfferAsync(Guid offerId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, item_definition_id, display_name, description, price, is_enabled,
                   available_from, available_until, purchase_limit_per_identity, sort_order
            FROM shop_offers
            WHERE id = @id;
            """,
            connection);
        command.Parameters.AddWithValue("id", offerId);
        await using var reader = await command.ExecuteReaderAsync(TestToken);
        if (!await reader.ReadAsync(TestToken))
        {
            return null;
        }

        return new PersistedOffer(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt64(4),
            reader.GetBoolean(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.GetInt32(9));
    }

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

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestToken);
        return connection;
    }

    private async Task ResetDatabaseAsync() => await database.ResetDatabaseAsync(TestToken);

    private static async Task<ShopOfferManagementResponse> ReadManagementOfferAsync(
        HttpResponseMessage response)
    {
        var offer = await response.Content
            .ReadFromJsonAsync<ShopOfferManagementResponse>(TestToken);
        return offer ?? throw new Xunit.Sdk.XunitException(
            "Expected a management offer response.");
    }

    private void SkipIfDatabaseIsUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static DateTimeOffset UtcNow()
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
        string? Description,
        long Price,
        bool IsEnabled,
        DateTimeOffset? AvailableFromUtc,
        DateTimeOffset? AvailableUntilUtc,
        int? PurchaseLimitPerIdentity,
        int SortOrder = 0);

    private sealed record PersistedOffer(
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
}
