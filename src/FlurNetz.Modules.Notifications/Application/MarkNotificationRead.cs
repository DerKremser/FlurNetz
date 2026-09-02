using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Markiert eine persönliche Notification idempotent als gelesen.
/// </summary>
public sealed class MarkNotificationRead
{
    private readonly ICommunityNotificationStore store;
    private readonly IClock clock;

    public MarkNotificationRead(ICommunityNotificationStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    public Task<bool> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default) =>
        store.MarkReadAsync(
            CommunityIdentityId.Create(communityIdentityId.Value),
            NotificationId.Create(notificationId.Value),
            CanonicalizeToPostgreSqlMicroseconds(clock.UtcNow),
            cancellationToken);

    private static DateTimeOffset CanonicalizeToPostgreSqlMicroseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var excessTicks = utc.Ticks % TimeSpan.TicksPerMicrosecond;
        return excessTicks == 0 ? utc : utc.AddTicks(-excessTicks);
    }
}
