using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Listet die persönliche Notification-Inbox newest-first mit Keyset-Pagination.
/// </summary>
public sealed class ListNotificationsForIdentity
{
    public const int DefaultPageSize = 50;
    public const int MinimumPageSize = 1;
    public const int MaximumPageSize = 100;

    private readonly ICommunityNotificationStore store;

    public ListNotificationsForIdentity(ICommunityNotificationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public async Task<CommunityNotificationPage> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        NotificationInboxCursor? cursor = null,
        bool unreadOnly = false,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        EnsureValidPageSize(pageSize);

        if (cursor is not null
            && (cursor.CommunityIdentityId != validIdentityId || cursor.UnreadOnly != unreadOnly))
        {
            throw new ArgumentException(
                "Der Notification-Cursor gehört nicht zur angefragten Identität oder zum Filter.",
                nameof(cursor));
        }

        var notifications = await store.ListForIdentityAsync(
                validIdentityId,
                cursor,
                unreadOnly,
                pageSize + 1,
                cancellationToken)
            .ConfigureAwait(false);

        var hasMore = notifications.Count > pageSize;
        var items = hasMore
            ? notifications.Take(pageSize).ToArray()
            : notifications.ToArray();
        var nextCursor = hasMore
            ? new NotificationInboxCursor(
                validIdentityId,
                unreadOnly,
                items[^1].CreatedAtUtc,
                items[^1].Id)
            : null;

        return new CommunityNotificationPage(items, nextCursor);
    }

    private static void EnsureValidPageSize(int pageSize)
    {
        if (pageSize is < MinimumPageSize or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Die Seitengröße muss zwischen {MinimumPageSize} und {MaximumPageSize} liegen.");
        }
    }
}
