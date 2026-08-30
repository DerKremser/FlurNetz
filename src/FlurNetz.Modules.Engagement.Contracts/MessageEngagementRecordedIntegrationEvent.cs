using FlurNetz.Messaging.Integration;

namespace FlurNetz.Modules.Engagement.Contracts;

/// <summary>
/// Bezeichnet die fachliche Tatsache, dass eine normalisierte Message-Aktivität aufgezeichnet wurde.
/// </summary>
/// <remarks>
/// Der Vertrag gehört zu Engagement, weil Engagement diese Tatsache besitzt. Er beschreibt
/// bewusst keinen Progressionsbefehl und enthält deshalb weder XP noch andere downstream-
/// spezifische Semantik. Die Guid ist ausschließlich die bereits aufgelöste interne
/// CommunityIdentityId; externe Plattformdaten gehören nicht in diesen Vertrag.
/// </remarks>
public sealed record MessageEngagementRecordedIntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Stabiler logischer Typ der Integration-Event-Nachricht.
    /// </summary>
    public const string MessageType = "engagement.message-recorded";

    /// <summary>
    /// Version des stabilen Payload-Schemas.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// Erstellt das Event für eine bereits intern aufgelöste Community-Identität.
    /// </summary>
    /// <param name="communityIdentityId">Die nicht leere interne CommunityIdentityId.</param>
    /// <exception cref="ArgumentException">Wenn die Identität leer ist.</exception>
    public MessageEngagementRecordedIntegrationEvent(Guid communityIdentityId)
    {
        if (communityIdentityId == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Community-Identity-ID des Engagement-Events darf nicht leer sein.",
                nameof(communityIdentityId));
        }

        CommunityIdentityId = communityIdentityId;
    }

    /// <summary>
    /// Liefert die interne Community-Identity-ID der aufgezeichneten Message.
    /// </summary>
    public Guid CommunityIdentityId { get; }
}
