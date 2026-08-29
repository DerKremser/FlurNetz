namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Stellt die In-Process-Verteilung interner Domain Events bereit.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Liefert ein Ereignis an alle dafür registrierten Handler.
    /// </summary>
    /// <param name="event">Das interne Ereignis.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Verarbeitung.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Verteilung.</returns>
    /// <remarks>
    /// Handler werden in Registrierungsreihenfolge sequenziell ausgeführt. Der erste Fehler
    /// beendet die Verteilung und wird an den Aufrufer weitergegeben; ein fehlender Handler
    /// ist ein gültiger No-op.
    /// </remarks>
    Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
}
