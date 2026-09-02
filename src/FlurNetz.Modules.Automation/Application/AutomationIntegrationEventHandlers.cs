using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Expliziter Automation-Consumer für aufgezeichnete Engagement-Nachrichten.</summary>
public sealed class EngagementMessageRecordedAutomationConsumer : IIntegrationEventHandler<MessageEngagementRecordedIntegrationEvent>
{
    /// <summary>Stabile Inbox-Identität des Consumers.</summary>
    public const string ConsumerName = "automation.engagement-message-recorded";

    private readonly ExecuteAutomationTrigger execute;

    /// <summary>Erstellt den Consumer.</summary>
    public EngagementMessageRecordedAutomationConsumer(ExecuteAutomationTrigger execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = execute;
    }

    /// <inheritdoc />
    public Task HandleAsync(MessageEngagementRecordedIntegrationEvent @event, IntegrationEventHandlerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = new AutomationTriggerSnapshot(
            context.MessageId,
            context.MessageType,
            context.SchemaVersion,
            context.OccurredAtUtc,
            @event.CommunityIdentityId);
        return execute.ExecuteAsync(snapshot, context.Connection, context.Transaction, cancellationToken);
    }
}

/// <summary>Expliziter Automation-Consumer für abgeschlossene Shop-Käufe.</summary>
public sealed class ShopPurchaseCompletedAutomationConsumer : IIntegrationEventHandler<ShopPurchaseCompletedIntegrationEvent>
{
    /// <summary>Stabile Inbox-Identität des Consumers.</summary>
    public const string ConsumerName = "automation.shop-purchase-completed";

    private readonly ExecuteAutomationTrigger execute;

    /// <summary>Erstellt den Consumer.</summary>
    public ShopPurchaseCompletedAutomationConsumer(ExecuteAutomationTrigger execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = execute;
    }

    /// <inheritdoc />
    public Task HandleAsync(ShopPurchaseCompletedIntegrationEvent @event, IntegrationEventHandlerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = new AutomationTriggerSnapshot(
            context.MessageId,
            context.MessageType,
            context.SchemaVersion,
            context.OccurredAtUtc,
            @event.CommunityIdentityId,
            @event.ShopPurchaseId,
            @event.ShopOfferId,
            @event.ItemDefinitionId,
            @event.PricePaid,
            @event.PurchasedAtUtc);
        return execute.ExecuteAsync(snapshot, context.Connection, context.Transaction, cancellationToken);
    }
}
