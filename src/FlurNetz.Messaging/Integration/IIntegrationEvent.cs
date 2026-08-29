namespace FlurNetz.Messaging.Integration;

/// <summary>
/// Markiert eine serialisierbare technische Nachricht für Kommunikation zwischen Modulgrenzen.
/// </summary>
/// <remarks>
/// Die technische Identität, Version und Zustellinformation liegen im Envelope. Das Event
/// selbst enthält ausschließlich seine Payload und muss keine MessageId duplizieren.
/// </remarks>
public interface IIntegrationEvent
{
}
