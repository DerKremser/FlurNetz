using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Lädt eine Notification, optional innerhalb der persönlichen Identity-Grenze.
/// </summary>
public sealed class GetNotification
{
    private readonly ICommunityNotificationStore store;

    public GetNotification(ICommunityNotificationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public Task<CommunityNotification?> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default) =>
        store.GetForIdentityAsync(
            CommunityIdentityId.Create(communityIdentityId.Value),
            NotificationId.Create(notificationId.Value),
            cancellationToken);

    public Task<CommunityNotification?> ExecuteAsync(
        NotificationId notificationId,
        CancellationToken cancellationToken = default) =>
        store.GetAsync(NotificationId.Create(notificationId.Value), cancellationToken);
}
