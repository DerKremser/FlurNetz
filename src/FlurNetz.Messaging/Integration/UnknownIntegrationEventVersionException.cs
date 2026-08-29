namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Kennzeichnet eine nicht registrierte Version eines bekannten Integration-Event-Typs.
/// </summary>
public sealed class UnknownIntegrationEventVersionException : InvalidOperationException
{
    /// <summary>
    /// Erstellt einen Fehler für eine unbekannte Nachrichtenversion.
    /// </summary>
    /// <param name="messageType">Der bekannte logische Nachrichtentyp.</param>
    /// <param name="schemaVersion">Die unbekannte Version.</param>
    public UnknownIntegrationEventVersionException(string messageType, int schemaVersion)
        : base($"The integration event message type '{messageType}' has no registered schema version '{schemaVersion}'.")
    {
        MessageType = messageType;
        SchemaVersion = schemaVersion;
    }

    /// <summary>
    /// Gibt den logischen Nachrichtentyp zurück.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gibt die unbekannte Schema-Version zurück.
    /// </summary>
    public int SchemaVersion { get; }
}
