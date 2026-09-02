using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Identity- und filtergebundener Keyset-Cursor der persönlichen Notification-Inbox.
/// </summary>
public sealed record NotificationInboxCursor
{
    public NotificationInboxCursor(
        CommunityIdentityId communityIdentityId,
        bool unreadOnly,
        DateTimeOffset createdAtUtc,
        NotificationId notificationId)
    {
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        UnreadOnly = unreadOnly;
        CreatedAtUtc = EnsureValidUtc(createdAtUtc);
        NotificationId = NotificationId.Create(notificationId.Value);
    }

    public CommunityIdentityId CommunityIdentityId { get; }

    public bool UnreadOnly { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public NotificationId NotificationId { get; }

    public static NotificationInboxCursor Create(
        CommunityIdentityId communityIdentityId,
        bool unreadOnly,
        DateTimeOffset createdAtUtc,
        NotificationId notificationId) =>
        new(communityIdentityId, unreadOnly, createdAtUtc, notificationId);

    private static DateTimeOffset EnsureValidUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Notification-Cursor muss in UTC vorliegen.", nameof(value));
        }

        if (value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Notification-Cursor muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                nameof(value));
        }

        return value;
    }
}
