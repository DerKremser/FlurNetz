namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Löst logische Integration-Event-Typen und Versionen über explizite Registrierungen auf.
/// </summary>
public interface IIntegrationEventTypeRegistry
{
    /// <summary>
    /// Registriert eine eindeutige Zuordnung für einen Payload-Typ.
    /// </summary>
    /// <typeparam name="TEvent">Der zu registrierende Integration-Event-Typ.</typeparam>
    /// <param name="messageType">Stabiler logischer Nachrichtentyp.</param>
    /// <param name="schemaVersion">Positive Version des Payload-Schemas.</param>
    void Register<TEvent>(string messageType, int schemaVersion)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// Löst Typ und Version aus einer logischen Nachrichtenzuordnung auf.
    /// </summary>
    /// <param name="messageType">Logischer Nachrichtentyp.</param>
    /// <param name="schemaVersion">Schema-Version.</param>
    /// <returns>Der passende Descriptor.</returns>
    IntegrationEventDescriptor Resolve(string messageType, int schemaVersion);

    /// <summary>
    /// Löst den Descriptor für einen bereits bekannten CLR-Payload-Typ auf.
    /// </summary>
    /// <param name="clrType">Registrierter CLR-Typ.</param>
    /// <returns>Der passende Descriptor.</returns>
    IntegrationEventDescriptor Resolve(Type clrType);
}
