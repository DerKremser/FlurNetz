using System.Data.Common;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Stellt einem Integration-Event-Handler die gemeinsame Datenbanktransaktion bereit.
/// </summary>
/// <remarks>
/// Der Context kapselt die Persistence-Kapselung, gibt aber die standardisierten ADO.NET-
/// Connection- und Transaction-Verträge weiter. Ein Handler muss seine Business Writes
/// damit in genau dieselbe Transaktion wie die Inbox-Markierung ausführen.
/// </remarks>
public sealed class IntegrationEventHandlerContext
{
    private readonly PostgreSqlTransaction transaction;
    private readonly IntegrationEventEnvelope envelope;

    internal IntegrationEventHandlerContext(
        IntegrationEventEnvelope envelope,
        PostgreSqlTransaction transaction)
    {
        this.envelope = envelope;
        this.transaction = transaction;
    }

    /// <summary>
    /// Gibt die stabile Identität der aktuell verarbeiteten Nachricht zurück.
    /// </summary>
    public Guid MessageId => envelope.MessageId;

    /// <summary>
    /// Gibt den logischen Typ der aktuell verarbeiteten Nachricht zurück.
    /// </summary>
    public string MessageType => envelope.MessageType;

    /// <summary>
    /// Gibt die Schema-Version der aktuell verarbeiteten Nachricht zurück.
    /// </summary>
    public int SchemaVersion => envelope.SchemaVersion;

    /// <summary>
    /// Gibt den ursprünglichen Entstehungszeitpunkt der Nachricht in UTC zurück.
    /// </summary>
    public DateTimeOffset OccurredAtUtc => envelope.OccurredAtUtc;

    /// <summary>
    /// Gibt die optionale Korrelation der Nachricht zurück.
    /// </summary>
    public string? CorrelationId => envelope.CorrelationId;

    /// <summary>
    /// Gibt die optionale Ursache der Nachricht zurück.
    /// </summary>
    public string? CausationId => envelope.CausationId;

    /// <summary>
    /// Gibt die gemeinsame geöffnete Datenbankverbindung zurück.
    /// </summary>
    public DbConnection Connection => transaction.Connection;

    /// <summary>
    /// Gibt die gemeinsame Datenbanktransaktion zurück.
    /// </summary>
    public DbTransaction Transaction => transaction.Transaction;
}
