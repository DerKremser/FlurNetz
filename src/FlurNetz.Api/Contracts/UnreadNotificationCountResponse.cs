namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort des ungelesenen Notification-Zählers.
/// </summary>
public sealed record UnreadNotificationCountResponse(long UnreadCount);
