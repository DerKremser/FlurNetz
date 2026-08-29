namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Beschreibt eine explizite, stabile Consumer-Registrierung.
/// </summary>
public interface IIntegrationEventHandlerRegistration
{
    /// <summary>
    /// Gibt die persistente Consumer Identity zurück.
    /// </summary>
    string ConsumerName { get; }

    /// <summary>
    /// Gibt den konkreten Payload-Typ zurück.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Verarbeitet eine Payload innerhalb des vom Processor vorgegebenen Contexts.
    /// </summary>
    /// <param name="event">Die Payload.</param>
    /// <param name="context">Die Inbox-Transaktion.</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Verarbeitung.</returns>
    Task HandleAsync(
        IIntegrationEvent @event,
        IntegrationEventHandlerContext context,
        CancellationToken cancellationToken);
}
