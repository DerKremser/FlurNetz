using System.Net;
using System.Net.Http.Json;
using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Modules.Notifications.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>
/// Prüft die echte API-Inbox inklusive DTO-Projektion, Cursor und Identity-Isolation.
/// </summary>
public sealed class NotificationsApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    private static readonly DateTimeOffset FirstTime =
        new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task ListCursorRoundtripAndReadLifecycleAreExposedThroughApiDtos()
    {
        SkipIfDatabaseIsUnavailable();
        await database.ResetDatabaseAsync(TestToken);
        await using var factory = CreateFactory();
        using var host = new FlurNetzApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        var identity = CommunityIdentityId.New();
        var otherIdentity = CommunityIdentityId.New();
        var first = CreateNotification(identity, "first", FirstTime);
        var second = CreateNotification(identity, "second", FirstTime.AddMinutes(1), withSource: true);
        var other = CreateNotification(otherIdentity, "other", FirstTime.AddMinutes(2));
        var store = new CommunityNotificationStore(factory);
        await store.AddAsync(first, TestToken);
        await store.AddAsync(second, TestToken);
        await store.AddAsync(other, TestToken);

        var route = $"/api/identities/{identity.Value:D}/notifications";

        var firstPageResponse = await client.GetAsync($"{route}?pageSize=1", TestToken);
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<CommunityNotificationListResponse>(TestToken);
        Assert.NotNull(firstPage);
        Assert.Single(firstPage!.Items);
        Assert.Equal(second.Id.Value, firstPage.Items[0].Id);
        Assert.NotNull(firstPage.NextCursor);

        var secondPageResponse = await client.GetAsync(
            $"{route}?pageSize=1&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}",
            TestToken);
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<CommunityNotificationListResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!.Items);
        Assert.Equal(first.Id.Value, secondPage.Items[0].Id);
        Assert.Null(secondPage.NextCursor);
        Assert.Equal(first.Message, secondPage.Items[0].Message);
        Assert.Equal("shop.purchase", firstPage.Items[0].SourceType);

        var single = await client.GetFromJsonAsync<CommunityNotificationResponse>(
            $"{route}/{first.Id.Value:D}",
            TestToken);
        Assert.NotNull(single);
        Assert.False(single!.IsRead);

        var count = await client.GetFromJsonAsync<UnreadNotificationCountResponse>(
            $"{route}/unread-count",
            TestToken);
        Assert.Equal(2, count?.UnreadCount);

        var markRead = await client.PostAsync($"{route}/{first.Id.Value:D}/read", null, TestToken);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);
        var repeatedMarkRead = await client.PostAsync($"{route}/{first.Id.Value:D}/read", null, TestToken);
        Assert.Equal(HttpStatusCode.NoContent, repeatedMarkRead.StatusCode);
        count = await client.GetFromJsonAsync<UnreadNotificationCountResponse>($"{route}/unread-count", TestToken);
        Assert.Equal(1, count?.UnreadCount);

        var markUnread = await client.PostAsync($"{route}/{first.Id.Value:D}/unread", null, TestToken);
        Assert.Equal(HttpStatusCode.NoContent, markUnread.StatusCode);
        var markAll = await client.PostAsync($"{route}/read-all", null, TestToken);
        Assert.Equal(HttpStatusCode.OK, markAll.StatusCode);
        var markAllResult = await markAll.Content.ReadFromJsonAsync<MarkAllNotificationsReadResponse>(TestToken);
        Assert.Equal(2, markAllResult?.MarkedCount);
        count = await client.GetFromJsonAsync<UnreadNotificationCountResponse>($"{route}/unread-count", TestToken);
        Assert.Equal(0, count?.UnreadCount);
    }

    [Fact]
    public async Task ApiRejectsMalformedIdsCursorsPageSizesAndForeignNotifications()
    {
        SkipIfDatabaseIsUnavailable();
        await database.ResetDatabaseAsync(TestToken);
        await using var factory = CreateFactory();
        using var host = new FlurNetzApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        var identity = CommunityIdentityId.New();
        var otherIdentity = CommunityIdentityId.New();
        var notification = CreateNotification(identity, "owned", FirstTime);
        var secondNotification = CreateNotification(identity, "owned-second", FirstTime.AddMinutes(1));
        var store = new CommunityNotificationStore(factory);
        await store.AddAsync(notification, TestToken);
        await store.AddAsync(secondNotification, TestToken);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/identities/not-a-guid/notifications", TestToken)).StatusCode);
        var route = $"/api/identities/{identity.Value:D}/notifications";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"{route}?pageSize=0", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"{route}?cursor=malformed", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"{route}?unreadOnly=not-bool", TestToken)).StatusCode);
        var cursorResponse = await client.GetAsync($"{route}?pageSize=1", TestToken);
        var cursorPage = await cursorResponse.Content.ReadFromJsonAsync<CommunityNotificationListResponse>(TestToken);
        Assert.NotNull(cursorPage?.NextCursor);
        var otherRoute = $"/api/identities/{otherIdentity.Value:D}/notifications";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync(
                $"{otherRoute}?pageSize=1&cursor={Uri.EscapeDataString(cursorPage!.NextCursor!)}",
                TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync(
                $"{route}?pageSize=1&unreadOnly=true&cursor={Uri.EscapeDataString(cursorPage.NextCursor!)}",
                TestToken)).StatusCode);
        var emptyIdentity = CommunityIdentityId.New();
        var emptyList = await client.GetFromJsonAsync<CommunityNotificationListResponse>(
            $"/api/identities/{emptyIdentity.Value:D}/notifications",
            TestToken);
        Assert.NotNull(emptyList);
        Assert.Empty(emptyList!.Items);
        Assert.Null(emptyList.NextCursor);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/identities/{otherIdentity.Value:D}/notifications/{notification.Id.Value:D}", TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/identities/{otherIdentity.Value:D}/notifications/{notification.Id.Value:D}/read", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"{route}/{Guid.NewGuid():D}", TestToken)).StatusCode);
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private static CommunityNotification CreateNotification(
        CommunityIdentityId identity,
        string title,
        DateTimeOffset createdAtUtc,
        bool withSource = false) => CommunityNotification.Create(
            NotificationId.New(),
            identity,
            "system.notice",
            title,
            "Snapshot message",
            withSource ? new NotificationSourceReference("shop.purchase", "purchase-1") : null,
            createdAtUtc);

    private void SkipIfDatabaseIsUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
