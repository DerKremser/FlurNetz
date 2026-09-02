namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort des Mark-All-Read-Vorgangs.
/// </summary>
public sealed record MarkAllNotificationsReadResponse(long MarkedCount);
