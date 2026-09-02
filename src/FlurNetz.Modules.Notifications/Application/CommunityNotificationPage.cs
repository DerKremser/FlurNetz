using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Ergebnis einer stabil paginierten Notification-Inbox-Abfrage.
/// </summary>
public sealed class CommunityNotificationPage
{
    public CommunityNotificationPage(
        IReadOnlyList<CommunityNotification> items,
        NotificationInboxCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        NextCursor = nextCursor;
    }

    public IReadOnlyList<CommunityNotification> Items { get; }

    public NotificationInboxCursor? NextCursor { get; }
}
