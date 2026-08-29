using FlurNetz.Messaging.Integration;

namespace FlurNetz.Messaging.Serialization;

/// <summary>
/// Serialisiert und deserialisiert registrierte Integration-Event-Payloads sicher als UTF-8-JSON.
/// </summary>
public interface IIntegrationEventSerializer
{
    /// <summary>
    /// Serialisiert die Payload eines Envelopes anhand seiner Registry-Zuordnung.
    /// </summary>
    /// <param name="envelope">Envelope mit Payload und Metadaten.</param>
    /// <returns>Die JSON-Payload mit logischem Typ und Version.</returns>
    SerializedIntegrationEvent Serialize(IntegrationEventEnvelope envelope);

    /// <summary>
    /// Deserialisiert eine registrierte JSON-Payload anhand von Typ und Version.
    /// </summary>
    /// <param name="serializedEvent">Typ, Version und UTF-8-JSON.</param>
    /// <returns>Die typisierte Payload.</returns>
    IIntegrationEvent Deserialize(SerializedIntegrationEvent serializedEvent);
}
