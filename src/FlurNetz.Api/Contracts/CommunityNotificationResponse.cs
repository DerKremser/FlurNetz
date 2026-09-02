namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-eigene Projektion einer persönlichen Notification.
/// </summary>
public sealed record CommunityNotificationResponse(
    Guid Id,
    Guid CommunityIdentityId,
    string NotificationType,
    string Title,
    string? Message,
    string? SourceType,
    string? SourceId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead);
