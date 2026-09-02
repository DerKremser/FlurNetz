using System.Data.Common;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Tests;

public sealed class NotificationIdTests
{
    [Fact]
    public void CreateAndNewProduceStableNonEmptyIds()
    {
        var value = Guid.Parse("20f2b4f0-6b2e-4c0b-b7a2-a2786d7ce0ea");

        Assert.Equal(value, NotificationId.Create(value).Value);
        Assert.NotEqual(Guid.Empty, NotificationId.New().Value);
        Assert.Throws<ArgumentException>(() => NotificationId.Create(Guid.Empty));
    }
}

public sealed class NotificationSourceReferenceTests
{
    [Fact]
    public void CreateTrimsAndCanRepresentAbsence()
    {
        var reference = NotificationSourceReference.Create("  shop.purchase ", " purchase-1 ");

        Assert.NotNull(reference);
        Assert.Equal("shop.purchase", reference!.SourceType);
        Assert.Equal("purchase-1", reference.SourceId);
        Assert.Null(NotificationSourceReference.Create(null, null));
    }

    [Fact]
    public void CreateRejectsOnlyOnePartBlankValuesAndInvalidUnicode()
    {
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create("shop", null));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create(null, "purchase"));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create(" ", "purchase"));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create("shop", "\0purchase"));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create("shop", "\uD800"));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Create(
            new string('a', NotificationSourceReference.MaxSourceTypeLength + 1),
            "purchase"));
    }

    [Fact]
    public void RehydrateRejectsNonCanonicalValues()
    {
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Rehydrate(" shop", "purchase"));
        Assert.Throws<ArgumentException>(() => NotificationSourceReference.Rehydrate("shop", " purchase"));
    }
}

public sealed class CommunityNotificationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public void CreateCanonicalizesSnapshotValuesAndRehydratePreservesThem()
    {
        var identityId = CommunityIdentityId.New();
        var source = new NotificationSourceReference("shop.purchase", "purchase-1");
        var created = CommunityNotification.Create(
            NotificationId.New(),
            identityId,
            " shop.purchase-completed ",
            " Kauf abgeschlossen ",
            "  Erfolgreich gekauft  ",
            source,
            CreatedAtUtc);

        var rehydrated = CommunityNotification.Rehydrate(
            created.Id,
            created.CommunityIdentityId,
            created.NotificationType,
            created.Title,
            created.Message,
            created.SourceReference,
            created.CreatedAtUtc,
            created.ReadAtUtc);

        Assert.Equal("shop.purchase-completed", created.NotificationType);
        Assert.Equal("Kauf abgeschlossen", created.Title);
        Assert.Equal("Erfolgreich gekauft", created.Message);
        Assert.Equal(created.Id, rehydrated.Id);
        Assert.Equal(created.CommunityIdentityId, rehydrated.CommunityIdentityId);
        Assert.Equal(created.SourceReference, rehydrated.SourceReference);
        Assert.Equal(CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.False(created.IsRead);
    }

    [Fact]
    public void CreateRejectsWhitespaceMessageAndAllowsNullMessage()
    {
        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            "system.notice",
            "Hinweis",
            " \u2003\u00a0 ",
            null,
            CreatedAtUtc));

        var notification = CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            "system.notice",
            "Hinweis",
            null,
            null,
            CreatedAtUtc);
        Assert.Null(notification.Message);
        Assert.Null(notification.SourceReference);
    }

    [Fact]
    public void RehydrateRejectsNonCanonicalOrMalformedText()
    {
        Assert.Throws<ArgumentException>(() => CommunityNotification.Rehydrate(
            NotificationId.New(),
            CommunityIdentityId.New(),
            " notification",
            "Title",
            null,
            null,
            CreatedAtUtc,
            null));
        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            "system.notice",
            "Title\0",
            null,
            null,
            CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            "system.notice",
            "\uD800",
            null,
            null,
            CreatedAtUtc));
    }

    [Fact]
    public void UnicodeScalarLimitsApplyToSupplementaryCharacters()
    {
        var valid = CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            RepeatUnicodeScalar("😀", CommunityNotification.MaxNotificationTypeLength),
            RepeatUnicodeScalar("🧪", CommunityNotification.MaxTitleLength),
            RepeatUnicodeScalar("🚀", CommunityNotification.MaxMessageLength),
            null,
            CreatedAtUtc);

        Assert.Equal(CommunityNotification.MaxNotificationTypeLength, valid.NotificationType.EnumerateRunes().Count());
        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.New(),
            RepeatUnicodeScalar("😀", CommunityNotification.MaxNotificationTypeLength + 1),
            "Title",
            null,
            null,
            CreatedAtUtc));
    }

    [Fact]
    public void CreationAndReadTimestampsRequireUtcMicrosecondPrecision()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var subMicrosecond = CreatedAtUtc.AddTicks(1);

        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(), CommunityIdentityId.New(), "type", "title", null, null, nonUtc));
        Assert.Throws<ArgumentException>(() => CommunityNotification.Create(
            NotificationId.New(), CommunityIdentityId.New(), "type", "title", null, null, subMicrosecond));

        var notification = CreateNotification();
        Assert.Throws<ArgumentException>(() => notification.MarkRead(nonUtc));
        Assert.Throws<ArgumentException>(() => notification.MarkRead(subMicrosecond));
    }

    [Fact]
    public void MarkReadAndUnreadAreIdempotentAndPreserveFirstReadTime()
    {
        var notification = CreateNotification();
        var firstReadAt = CreatedAtUtc.AddHours(1);
        var secondReadAt = CreatedAtUtc.AddHours(2);

        Assert.True(notification.MarkRead(firstReadAt));
        Assert.False(notification.MarkRead(secondReadAt));
        Assert.Equal(firstReadAt, notification.ReadAtUtc);
        Assert.True(notification.MarkUnread());
        Assert.False(notification.MarkUnread());
        Assert.Null(notification.ReadAtUtc);
    }

    private static CommunityNotification CreateNotification() => CommunityNotification.Create(
        NotificationId.New(),
        CommunityIdentityId.New(),
        "system.notice",
        "Hinweis",
        null,
        null,
         CreatedAtUtc);

    private static string RepeatUnicodeScalar(string scalar, int count) =>
        string.Concat(Enumerable.Repeat(scalar, count));
}

public sealed class NotificationApplicationTests
{
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public async Task CreateUseCaseUsesClockAndStoreAndListUsesStableKeysetRules()
    {
        var store = new InMemoryNotificationStore();
        var create = new CreateNotification(store, new FixedClock(Now));
        var identityId = CommunityIdentityId.New();

        var notification = await create.ExecuteAsync(
            identityId,
            "system.notice",
            "Hinweis",
            "Text",
            cancellationToken: TestToken);

        Assert.Equal(Now, notification.CreatedAtUtc);
        Assert.Same(notification, Assert.Single(store.Items));

        var list = new ListNotificationsForIdentity(store);
        var page = await list.ExecuteAsync(identityId, pageSize: 1, cancellationToken: TestToken);
        Assert.Single(page.Items);
        Assert.Null(page.NextCursor);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => list.ExecuteAsync(
            identityId,
            pageSize: ListNotificationsForIdentity.MaximumPageSize + 1,
            cancellationToken: TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            identityId,
            new NotificationInboxCursor(CommunityIdentityId.New(), false, Now, notification.Id),
            cancellationToken: TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            identityId,
            new NotificationInboxCursor(identityId, true, Now, notification.Id),
            unreadOnly: false,
            cancellationToken: TestToken));
    }

    private static string RepeatUnicodeScalar(string scalar, int count) =>
        string.Concat(Enumerable.Repeat(scalar, count));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class InMemoryNotificationStore : ICommunityNotificationStore
    {
        public List<CommunityNotification> Items { get; } = [];

        public Task AddAsync(CommunityNotification notification, CancellationToken cancellationToken = default)
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddAsync(CommunityNotification notification, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default) =>
            AddAsync(notification, cancellationToken);

        public Task<CommunityNotification?> GetAsync(NotificationId notificationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == notificationId));

        public Task<CommunityNotification?> GetForIdentityAsync(CommunityIdentityId communityIdentityId, NotificationId notificationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == notificationId && item.CommunityIdentityId == communityIdentityId));

        public Task<IReadOnlyList<CommunityNotification>> ListForIdentityAsync(CommunityIdentityId communityIdentityId, NotificationInboxCursor? cursor, bool unreadOnly, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CommunityNotification>>(Items
                .Where(item => item.CommunityIdentityId == communityIdentityId && (!unreadOnly || !item.IsRead))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.Id.Value)
                .Take(take)
                .ToArray());

        public Task<long> CountUnreadForIdentityAsync(CommunityIdentityId communityIdentityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.LongCount(item => item.CommunityIdentityId == communityIdentityId && !item.IsRead));

        public Task<bool> MarkReadAsync(CommunityIdentityId communityIdentityId, NotificationId notificationId, DateTimeOffset readAtUtc, CancellationToken cancellationToken = default)
        {
            var item = Items.SingleOrDefault(item => item.CommunityIdentityId == communityIdentityId && item.Id == notificationId);
            return Task.FromResult(item is not null && (item.MarkRead(readAtUtc) || item.IsRead));
        }

        public Task<bool> MarkUnreadAsync(CommunityIdentityId communityIdentityId, NotificationId notificationId, CancellationToken cancellationToken = default)
        {
            var item = Items.SingleOrDefault(item => item.CommunityIdentityId == communityIdentityId && item.Id == notificationId);
            return Task.FromResult(item is not null && (item.MarkUnread() || !item.IsRead));
        }

        public Task<long> MarkAllReadAsync(CommunityIdentityId communityIdentityId, DateTimeOffset readAtUtc, CancellationToken cancellationToken = default)
        {
            var count = 0L;
            foreach (var item in Items.Where(item => item.CommunityIdentityId == communityIdentityId && !item.IsRead))
            {
                if (item.MarkRead(readAtUtc))
                {
                    count++;
                }
            }

            return Task.FromResult(count);
        }
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
