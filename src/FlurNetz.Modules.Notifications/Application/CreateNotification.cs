using System.Data.Common;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Erzeugt persönliche Notifications aus internen Systemprozessen.
/// </summary>
public sealed class CreateNotification
{
    private readonly ICommunityNotificationStore store;
    private readonly IClock clock;

    public CreateNotification(ICommunityNotificationStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    public async Task<CommunityNotification> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message = null,
        NotificationSourceReference? sourceReference = null,
        CancellationToken cancellationToken = default)
    {
        var notification = CreateDomainNotification(
            communityIdentityId,
            notificationType,
            title,
            message,
            sourceReference);
        await store.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        return notification;
    }

    /// <summary>
    /// Erzeugt eine Notification innerhalb einer vom Messaging-Processor bereitgestellten Transaktion.
    /// </summary>
    public async Task<CommunityNotification> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var notification = CreateDomainNotification(
            communityIdentityId,
            notificationType,
            title,
            message,
            sourceReference);
        await store.AddAsync(notification, connection, transaction, cancellationToken).ConfigureAwait(false);
        return notification;
    }

    private CommunityNotification CreateDomainNotification(
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference) =>
        CommunityNotification.Create(
            NotificationId.New(),
            CommunityIdentityId.Create(communityIdentityId.Value),
            notificationType,
            title,
            message,
            sourceReference,
            CanonicalizeToPostgreSqlMicroseconds(clock.UtcNow));

    private static DateTimeOffset CanonicalizeToPostgreSqlMicroseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var excessTicks = utc.Ticks % TimeSpan.TicksPerMicrosecond;
        return excessTicks == 0 ? utc : utc.AddTicks(-excessTicks);
    }
}
