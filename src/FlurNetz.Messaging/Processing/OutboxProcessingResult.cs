namespace FlurNetz.Messaging.Processing;

/// <summary>
/// Fasst einen expliziten Outbox-Processor-Lauf zusammen.
/// </summary>
/// <param name="ClaimedCount">Anzahl erfolgreich beanspruchter Nachrichten.</param>
/// <param name="ProcessedCount">Anzahl erfolgreich abgeschlossener Nachrichten.</param>
/// <param name="RetriedCount">Anzahl für einen späteren Versuch zurückgestellter Nachrichten.</param>
/// <param name="FailedCount">Anzahl als Poison/Failed markierter Nachrichten.</param>
/// <param name="DuplicateDeliveryCount">Anzahl durch die Inbox deduplizierter Consumer-Zustellungen.</param>
public sealed record OutboxProcessingResult(
    int ClaimedCount,
    int ProcessedCount,
    int RetriedCount,
    int FailedCount,
    int DuplicateDeliveryCount);
