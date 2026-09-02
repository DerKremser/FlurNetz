using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Markiert alle aktuell ungelesenen Notifications einer Identity mit einem einheitlichen Zeitpunkt.
/// </summary>
public sealed class MarkAllNotificationsRead
{
    private readonly ICommunityNotificationStore store;
    private readonly IClock clock;

    public MarkAllNotificationsRead(ICommunityNotificationStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    public Task<long> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default) =>
        store.MarkAllReadAsync(
            CommunityIdentityId.Create(communityIdentityId.Value),
            CanonicalizeToPostgreSqlMicroseconds(clock.UtcNow),
            cancellationToken);

    private static DateTimeOffset CanonicalizeToPostgreSqlMicroseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var excessTicks = utc.Ticks % TimeSpan.TicksPerMicrosecond;
        return excessTicks == 0 ? utc : utc.AddTicks(-excessTicks);
    }
}
