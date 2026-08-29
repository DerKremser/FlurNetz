using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Trennt technische Nachrichtenmetadaten von der fachlichen Integration-Event-Payload.
/// </summary>
/// <param name="MessageId">Stabile eindeutige Identität der Nachricht.</param>
/// <param name="MessageType">Expliziter logischer Nachrichtentyp.</param>
/// <param name="SchemaVersion">Explizite Version des Payload-Schemas.</param>
/// <param name="OccurredAtUtc">Zeitpunkt der Ereignisentstehung in UTC.</param>
/// <param name="Payload">Die fachliche Payload ohne technische Envelope-Felder.</param>
/// <param name="CorrelationId">Optionale technische Korrelation.</param>
/// <param name="CausationId">Optionale technische Ursache.</param>
public sealed record IntegrationEventEnvelope
{
    /// <summary>
    /// Erstellt einen validierten Nachrichten-Envelope.
    /// </summary>
    public IntegrationEventEnvelope(
        Guid MessageId,
        string MessageType,
        int SchemaVersion,
        DateTimeOffset OccurredAtUtc,
        IIntegrationEvent Payload,
        string? CorrelationId = null,
        string? CausationId = null)
    {
        if (MessageId == Guid.Empty)
        {
            throw new ArgumentException("The message id cannot be empty.", nameof(MessageId));
        }

        if (SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "The schema version must be positive.");
        }

        if (OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The occurrence timestamp must be in UTC.", nameof(OccurredAtUtc));
        }

        this.MessageId = MessageId;
        this.MessageType = Guard.NotNullOrWhiteSpace(MessageType, nameof(MessageType));
        this.SchemaVersion = SchemaVersion;
        this.OccurredAtUtc = OccurredAtUtc;
        this.Payload = Guard.NotNull(Payload, nameof(Payload));
        CorrelationId = string.IsNullOrWhiteSpace(CorrelationId) ? null : CorrelationId;
        CausationId = string.IsNullOrWhiteSpace(CausationId) ? null : CausationId;
    }

    /// <summary>
    /// Gibt die stabile eindeutige Identität der Nachricht zurück.
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// Gibt den stabilen logischen Nachrichtentyp zurück.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gibt die Version des serialisierten Payload-Schemas zurück.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gibt den Erstellungszeitpunkt der Nachricht in UTC zurück.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Gibt die fachliche Payload zurück.
    /// </summary>
    public IIntegrationEvent Payload { get; }

    /// <summary>
    /// Gibt die optionale technische Korrelationsidentität zurück.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gibt die optionale technische Ursache dieser Nachricht zurück.
    /// </summary>
    public string? CausationId { get; }
}
