using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Progression.Application;

/// <summary>
/// Interpretiert eine aufgezeichnete Message als einen Progressionsschritt.
/// </summary>
/// <remarks>
/// Das Event ist eine Tatsache aus Engagement und kein Befehl zur XP-Vergabe. Die Policy
/// liegt deshalb ausschließlich hier in Progression. Der Handler reicht die vom Inbox-
/// Processor bereitgestellte Verbindung und Transaktion durch, damit Inbox-Eintrag und
/// XP-Write gemeinsam committen oder gemeinsam zurückgerollt werden.
/// </remarks>
public sealed class MessageEngagementRecordedIntegrationEventHandler
    : IIntegrationEventHandler<MessageEngagementRecordedIntegrationEvent>
{
    /// <summary>
    /// Stabile Consumer Identity für die Inbox-Idempotenzgrenze.
    /// </summary>
    public const string ConsumerName = "progression.message-engagement-xp";

    /// <summary>
    /// Fachliche Progressionsregel für eine normalisierte Message-Aktivität.
    /// </summary>
    public const long MessageExperiencePoints = 1;

    private readonly ICommunityProgressionStore store;

    /// <summary>
    /// Erstellt den Progression-Consumer.
    /// </summary>
    /// <param name="store">Der transaction-aware Progression-Store.</param>
    /// <exception cref="ArgumentNullException">Wenn der Store fehlt.</exception>
    public MessageEngagementRecordedIntegrationEventHandler(ICommunityProgressionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc />
    public Task HandleAsync(
        MessageEngagementRecordedIntegrationEvent @event,
        IntegrationEventHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        var communityIdentityId = CommunityIdentityId.Create(@event.CommunityIdentityId);
        return store.GrantExperienceAsync(
            communityIdentityId,
            MessageExperiencePoints,
            context.Connection,
            context.Transaction,
            cancellationToken);
    }
}
