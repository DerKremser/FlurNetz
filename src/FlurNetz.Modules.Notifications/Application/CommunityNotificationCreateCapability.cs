using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>Adapter der öffentlichen Create-Capability auf den bestehenden Notification-Kern.</summary>
public sealed class CommunityNotificationCreateCapability : ICommunityNotificationCreate
{
    private readonly CreateNotification createNotification;

    /// <summary>Erstellt den Adapter ohne parallele Persistenzlogik.</summary>
    public CommunityNotificationCreateCapability(CreateNotification createNotification)
    {
        ArgumentNullException.ThrowIfNull(createNotification);
        this.createNotification = createNotification;
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        string sourceType,
        string sourceId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var sourceReference = new NotificationSourceReference(sourceType, sourceId);
        _ = await createNotification.ExecuteAsync(
                communityIdentityId,
                notificationType,
                title,
                message,
                sourceReference,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
