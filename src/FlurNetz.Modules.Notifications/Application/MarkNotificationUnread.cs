using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Markiert eine persönliche Notification idempotent als ungelesen.
/// </summary>
public sealed class MarkNotificationUnread
{
    private readonly ICommunityNotificationStore store;

    public MarkNotificationUnread(ICommunityNotificationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default) =>
        store.MarkUnreadAsync(
            CommunityIdentityId.Create(communityIdentityId.Value),
            NotificationId.Create(notificationId.Value),
            cancellationToken);
}
