namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Verbindet einen konkreten Domain-Event-Handler explizit mit seinem Ereignistyp.
/// </summary>
/// <typeparam name="TEvent">Der konkrete Domain-Event-Typ.</typeparam>
public sealed class DomainEventHandlerRegistration<TEvent> : IDomainEventHandlerRegistration
    where TEvent : IDomainEvent
{
    private readonly IDomainEventHandler<TEvent> handler;

    /// <summary>
    /// Erstellt eine Handler-Registrierung.
    /// </summary>
    /// <param name="handler">Der zu registrierende Handler.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="handler"/> fehlt.</exception>
    public DomainEventHandlerRegistration(IDomainEventHandler<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        this.handler = handler;
    }

    /// <inheritdoc />
    public Type EventType => typeof(TEvent);

    /// <inheritdoc />
    public Task HandleAsync(IDomainEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event is not TEvent typedEvent)
        {
            throw new InvalidOperationException(
                $"The event type '{@event.GetType().FullName}' does not match registered type '{typeof(TEvent).FullName}'.");
        }

        return handler.HandleAsync(typedEvent, cancellationToken);
    }
}
