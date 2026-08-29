namespace FlurNetz.Messaging.Domain;

/// <summary>
/// Markiert ein internes Ereignis für Reaktionen innerhalb des Prozesses.
/// </summary>
/// <remarks>
/// Domain Events sind bewusst nicht serialisierbar und werden nicht in der Outbox persistiert.
/// Dadurch bleibt die interne Reaktion von der stabilen Nachrichtenkommunikation zwischen
/// Modulgrenzen getrennt.
/// </remarks>
public interface IDomainEvent
{
}
