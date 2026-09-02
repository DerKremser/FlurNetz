using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Notifications.Application;

/// <summary>
/// Erzeugt den expliziten V1-Snapshot für einen erfolgreich abgeschlossenen Shop-Kauf.
/// </summary>
/// <remarks>
/// Die Policy verwendet ausschließlich die Event-Payload. Shop-Tabellen, Shop-Stores und
/// die Shop-Implementierung werden bewusst nicht angesprochen.
/// </remarks>
public sealed class ShopPurchaseCompletedIntegrationEventHandler
    : IIntegrationEventHandler<ShopPurchaseCompletedIntegrationEvent>
{
    public const string ConsumerName = "notifications.shop-purchase";
    public const string NotificationType = "shop.purchase-completed";
    public const string NotificationTitle = "Shop-Kauf abgeschlossen";
    public const string NotificationMessage = "Dein Shop-Kauf wurde erfolgreich abgeschlossen.";
    public const string SourceType = "shop.purchase";

    private readonly CreateNotification createNotification;

    public ShopPurchaseCompletedIntegrationEventHandler(CreateNotification createNotification)
    {
        ArgumentNullException.ThrowIfNull(createNotification);
        this.createNotification = createNotification;
    }

    public async Task HandleAsync(
        ShopPurchaseCompletedIntegrationEvent @event,
        IntegrationEventHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        var identityId = CommunityIdentityId.Create(@event.CommunityIdentityId);
        var sourceReference = new NotificationSourceReference(
            SourceType,
            @event.ShopPurchaseId.ToString("D"));

        _ = await createNotification.ExecuteAsync(
                identityId,
                NotificationType,
                NotificationTitle,
                NotificationMessage,
                sourceReference,
                context.Connection,
                context.Transaction,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
