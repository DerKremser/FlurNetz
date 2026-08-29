using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Hält die ausdrücklich registrierten logischen Integration-Event-Typen und Versionen.
/// </summary>
/// <remarks>
/// Die Registry durchsucht keine Assemblies und verwendet keine CLR-Namen als Wire-Format.
/// Dadurch bleiben persistierte Nachrichten von Refactorings und Assembly-Versionen unabhängig.
/// </remarks>
public sealed class IntegrationEventTypeRegistry : IIntegrationEventTypeRegistry
{
    private readonly Dictionary<(string MessageType, int SchemaVersion), IntegrationEventDescriptor> byMessage = [];
    private readonly Dictionary<Type, IntegrationEventDescriptor> byClrType = [];

    /// <inheritdoc />
    public void Register<TEvent>(string messageType, int schemaVersion)
        where TEvent : IIntegrationEvent
    {
        var descriptor = new IntegrationEventDescriptor(messageType, schemaVersion, typeof(TEvent));
        var messageKey = (descriptor.MessageType, descriptor.SchemaVersion);
        if (byMessage.ContainsKey(messageKey))
        {
            throw new InvalidOperationException(
                $"The integration event message type '{descriptor.MessageType}' and version '{descriptor.SchemaVersion}' are already registered.");
        }

        if (byClrType.ContainsKey(descriptor.ClrType))
        {
            throw new InvalidOperationException(
                $"The CLR integration event type '{descriptor.ClrType.FullName}' is already registered.");
        }

        byMessage.Add(messageKey, descriptor);
        byClrType.Add(descriptor.ClrType, descriptor);
    }

    /// <inheritdoc />
    public IntegrationEventDescriptor Resolve(string messageType, int schemaVersion)
    {
        var normalizedMessageType = Guard.NotNullOrWhiteSpace(messageType, nameof(messageType));
        if (!byMessage.TryGetValue((normalizedMessageType, schemaVersion), out var descriptor))
        {
            if (!byMessage.Keys.Any(key => string.Equals(key.MessageType, normalizedMessageType, StringComparison.Ordinal)))
            {
                throw new UnknownIntegrationEventTypeException(normalizedMessageType);
            }

            throw new UnknownIntegrationEventVersionException(normalizedMessageType, schemaVersion);
        }

        return descriptor;
    }

    /// <inheritdoc />
    public IntegrationEventDescriptor Resolve(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return byClrType.TryGetValue(clrType, out var descriptor)
            ? descriptor
            : throw new UnknownIntegrationEventTypeException(clrType.FullName ?? clrType.Name);
    }
}
