using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.Messaging.Serialization;

/// <summary>
/// Enthält JSON-Payload sowie deren explizite logische Typ- und Versionsmetadaten.
/// </summary>
public sealed record SerializedIntegrationEvent
{
    /// <summary>
    /// Erstellt eine serialisierte Integration Event-Nachricht.
    /// </summary>
    /// <param name="MessageType">Logischer Nachrichtentyp aus der Registry.</param>
    /// <param name="SchemaVersion">Schema-Version aus der Registry.</param>
    /// <param name="Payload">UTF-8-kodierte JSON-Payload.</param>
    public SerializedIntegrationEvent(string MessageType, int SchemaVersion, byte[] Payload)
    {
        this.MessageType = Guard.NotNullOrWhiteSpace(MessageType, nameof(MessageType));
        if (SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "The schema version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(Payload);
        this.SchemaVersion = SchemaVersion;
        this.Payload = Payload.ToArray();
    }

    /// <summary>
    /// Gibt den logischen Nachrichtentyp zurück.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gibt die Schema-Version zurück.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gibt die unveränderliche Kopie der UTF-8-JSON-Payload zurück.
    /// </summary>
    public byte[] Payload { get; }
}
