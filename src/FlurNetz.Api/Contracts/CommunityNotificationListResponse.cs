namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort einer paginierten persönlichen Notification-Inbox.
/// </summary>
public sealed record CommunityNotificationListResponse(
    IReadOnlyList<CommunityNotificationResponse> Items,
    string? NextCursor);
