namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Verarbeitet eine Integration-Event-Payload innerhalb einer Inbox-Transaktion.
/// </summary>
/// <typeparam name="TEvent">Der konkrete Integration-Event-Typ.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Führt den Consumer-Effekt aus.
    /// </summary>
    /// <param name="event">Die deserialisierte Payload.</param>
    /// <param name="context">Die gemeinsame Transaktion für Inbox und Business-Write.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Verarbeitung.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Verarbeitung.</returns>
    Task HandleAsync(
        TEvent @event,
        IntegrationEventHandlerContext context,
        CancellationToken cancellationToken = default);
}
