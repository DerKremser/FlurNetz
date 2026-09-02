using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Ermittelt die Anzahl ungelesener Notifications einer Identity.
/// </summary>
public sealed class GetUnreadNotificationCount
{
    private readonly ICommunityNotificationStore store;

    public GetUnreadNotificationCount(ICommunityNotificationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public Task<long> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default) =>
        store.CountUnreadForIdentityAsync(
            CommunityIdentityId.Create(communityIdentityId.Value),
            cancellationToken);
}
