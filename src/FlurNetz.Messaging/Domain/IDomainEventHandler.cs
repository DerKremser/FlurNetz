namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Verarbeitet ein internes Domain Event asynchron.
/// </summary>
/// <typeparam name="TEvent">Der konkrete Domain-Event-Typ.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Verarbeitet das Ereignis.
    /// </summary>
    /// <param name="event">Das zu verarbeitende Ereignis.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Verarbeitung.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Verarbeitung.</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
