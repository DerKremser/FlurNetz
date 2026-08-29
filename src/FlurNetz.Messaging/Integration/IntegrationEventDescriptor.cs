using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Beschreibt die explizite Zuordnung eines logischen Nachrichtentyps zu einem CLR-Payload-Typ.
/// </summary>
public sealed record IntegrationEventDescriptor
{
    /// <summary>
    /// Erstellt einen Descriptor für Typ und Schema-Version.
    /// </summary>
    /// <param name="MessageType">Stabiler logischer Nachrichtentyp.</param>
    /// <param name="SchemaVersion">Positive Schema-Version.</param>
    /// <param name="ClrType">Explizit registrierter CLR-Payload-Typ.</param>
    public IntegrationEventDescriptor(string MessageType, int SchemaVersion, Type ClrType)
    {
        this.MessageType = Guard.NotNullOrWhiteSpace(MessageType, nameof(MessageType));
        if (SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "The schema version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(ClrType);
        if (!typeof(IIntegrationEvent).IsAssignableFrom(ClrType))
        {
            throw new ArgumentException("The CLR type must implement IIntegrationEvent.", nameof(ClrType));
        }

        this.SchemaVersion = SchemaVersion;
        this.ClrType = ClrType;
    }

    /// <summary>
    /// Gibt den stabilen logischen Nachrichtentyp zurück.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gibt die Schema-Version zurück.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gibt den explizit registrierten CLR-Payload-Typ zurück.
    /// </summary>
    public Type ClrType { get; }
}
