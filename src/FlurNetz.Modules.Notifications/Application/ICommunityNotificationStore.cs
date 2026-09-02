using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Gezielte Persistenzgrenze für persönliche Notifications.
/// </summary>
public interface ICommunityNotificationStore
{
    Task AddAsync(
        CommunityNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fügt eine Notification in eine bereits laufende Inbox-Transaktion ein.
    /// </summary>
    /// <remarks>
    /// Diese Methode führt keinen Commit aus. Der Messaging-Processor besitzt die gemeinsame
    /// Transaktionsgrenze von Inbox-Markierung und Notification-Write.
    /// </remarks>
    Task AddAsync(
        CommunityNotification notification,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<CommunityNotification?> GetAsync(
        NotificationId notificationId,
        CancellationToken cancellationToken = default);

    Task<CommunityNotification?> GetForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityNotification>> ListForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        NotificationInboxCursor? cursor,
        bool unreadOnly,
        int take,
        CancellationToken cancellationToken = default);

    Task<long> CountUnreadForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> MarkUnreadAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default);

    Task<long> MarkAllReadAsync(
        CommunityIdentityId communityIdentityId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default);
}
