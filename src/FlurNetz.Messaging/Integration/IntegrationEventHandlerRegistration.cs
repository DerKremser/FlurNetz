using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Verbindet einen Integration-Event-Handler mit einer stabilen Consumer Identity.
/// </summary>
/// <typeparam name="TEvent">Der konkrete Integration-Event-Typ.</typeparam>
public sealed class IntegrationEventHandlerRegistration<TEvent> : IIntegrationEventHandlerRegistration
    where TEvent : IIntegrationEvent
{
    private readonly IIntegrationEventHandler<TEvent> handler;

    /// <summary>
    /// Erstellt eine Consumer-Registrierung.
    /// </summary>
    /// <param name="consumerName">Stabiler Name des Consumers, unabhängig vom CLR-Klassennamen.</param>
    /// <param name="handler">Der zu registrierende Handler.</param>
    /// <exception cref="ArgumentNullException">Wenn der Handler fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn der Consumer-Name leer ist.</exception>
    public IntegrationEventHandlerRegistration(
        string consumerName,
        IIntegrationEventHandler<TEvent> handler)
    {
        ConsumerName = Guard.NotNullOrWhiteSpace(consumerName, nameof(consumerName));
        ArgumentNullException.ThrowIfNull(handler);
        this.handler = handler;
    }

    /// <inheritdoc />
    public string ConsumerName { get; }

    /// <inheritdoc />
    public Type EventType => typeof(TEvent);

    /// <inheritdoc />
    public Task HandleAsync(
        IIntegrationEvent @event,
        IntegrationEventHandlerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        if (@event is not TEvent typedEvent)
        {
            throw new InvalidOperationException(
                $"The event type '{@event.GetType().FullName}' does not match registered type '{typeof(TEvent).FullName}'.");
        }

        return handler.HandleAsync(typedEvent, context, cancellationToken);
    }
}
