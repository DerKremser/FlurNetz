namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Beschreibt eine explizite, typisierte Domain-Event-Handler-Registrierung.
/// </summary>
public interface IDomainEventHandlerRegistration
{
    /// <summary>
    /// Gibt den konkreten Ereignistyp dieser Registrierung zurück.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Ruft den registrierten Handler für ein Ereignis auf.
    /// </summary>
    /// <param name="event">Das zu verarbeitende Ereignis.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Verarbeitung.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Verarbeitung.</returns>
    Task HandleAsync(IDomainEvent @event, CancellationToken cancellationToken);
}
