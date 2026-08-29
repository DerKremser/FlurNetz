namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Verteilt Domain Events deterministisch und ausschließlich innerhalb des Prozesses.
/// </summary>
/// <remarks>
/// Die Registrierungen werden einmalig materialisiert. Dadurch gibt es keine implizite
/// Reflection-Suche oder einen Service Locator; ihre Reihenfolge ist die Ausführungsreihenfolge.
/// Die sequenzielle Verarbeitung macht Seiteneffekte und Fehlergrenzen nachvollziehbar.
/// </remarks>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventHandlerRegistration> registrations;

    /// <summary>
    /// Erstellt einen Dispatcher mit explizit geordneten Handler-Registrierungen.
    /// </summary>
    /// <param name="registrations">Die Registrierungen in gewünschter Ausführungsreihenfolge.</param>
    /// <exception cref="ArgumentNullException">Wenn die Sammlung fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn die Sammlung eine Null-Registrierung enthält.</exception>
    public DomainEventDispatcher(IEnumerable<IDomainEventHandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        this.registrations = registrations.ToArray();

        if (this.registrations.Any(registration => registration is null))
        {
            throw new ArgumentException("A domain event handler registration cannot be null.", nameof(registrations));
        }
    }

    /// <inheritdoc />
    public async Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        foreach (var registration in registrations.Where(registration => registration.EventType == @event.GetType()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await registration.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }
}
