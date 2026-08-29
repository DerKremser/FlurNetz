using System.Text.Json;
using System.Text.Json.Serialization;
using FlurNetz.Messaging.Integration;

namespace FlurNetz.Messaging.Serialization;

/// <summary>
/// Implementiert die explizit typisierte Integration-Event-Serialisierung mit System.Text.Json.
/// </summary>
/// <remarks>
/// Die Registry entscheidet allein über den erlaubten CLR-Typ. Es gibt keine polymorphe
/// Deserialisierung aus Payload-Daten und keine Persistierung von AssemblyQualifiedName.
/// </remarks>
public sealed class IntegrationEventJsonSerializer : IIntegrationEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly IIntegrationEventTypeRegistry registry;

    /// <summary>
    /// Erstellt einen Serializer für eine explizite Typ-Registry.
    /// </summary>
    /// <param name="registry">Registry der erlaubten Typen und Versionen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Registry fehlt.</exception>
    public IntegrationEventJsonSerializer(IIntegrationEventTypeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        this.registry = registry;
    }

    /// <inheritdoc />
    public SerializedIntegrationEvent Serialize(IntegrationEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var descriptor = registry.Resolve(envelope.MessageType, envelope.SchemaVersion);
        if (!descriptor.ClrType.IsInstanceOfType(envelope.Payload))
        {
            throw new InvalidOperationException(
                $"The payload CLR type '{envelope.Payload.GetType().FullName}' does not match registered message type '{envelope.MessageType}' and version '{envelope.SchemaVersion}'.");
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope.Payload, descriptor.ClrType, SerializerOptions);
        return new SerializedIntegrationEvent(envelope.MessageType, envelope.SchemaVersion, payload);
    }

    /// <inheritdoc />
    public IIntegrationEvent Deserialize(SerializedIntegrationEvent serializedEvent)
    {
        ArgumentNullException.ThrowIfNull(serializedEvent);
        var descriptor = registry.Resolve(serializedEvent.MessageType, serializedEvent.SchemaVersion);
        var payload = JsonSerializer.Deserialize(serializedEvent.Payload, descriptor.ClrType, SerializerOptions);
        return payload as IIntegrationEvent
            ?? throw new JsonException(
                $"The JSON payload for '{serializedEvent.MessageType}' and version '{serializedEvent.SchemaVersion}' was null or had an invalid type.");
    }

    private static JsonSerializerOptions CreateSerializerOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };
}
