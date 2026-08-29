namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Kennzeichnet einen nicht registrierten logischen Integration-Event-Typ.
/// </summary>
public sealed class UnknownIntegrationEventTypeException : InvalidOperationException
{
    /// <summary>
    /// Erstellt einen Fehler für einen unbekannten Nachrichtentyp.
    /// </summary>
    /// <param name="messageType">Der unbekannte logische Nachrichtentyp.</param>
    public UnknownIntegrationEventTypeException(string messageType)
        : base($"The integration event message type '{messageType}' is not registered.")
    {
        MessageType = messageType;
    }

    /// <summary>
    /// Gibt den unbekannten Nachrichtentyp zurück.
    /// </summary>
    public string MessageType { get; }
}
