namespace FlurNetz.Api.Contracts;

public sealed record AdminEconomyAdjustmentRequest(
    long? Amount,
    string? Reason,
    Guid? RequestId);

public sealed record AdminEconomyAdjustmentResponse(
    Guid CommunityIdentityId,
    long Balance,
    bool AlreadyCompleted);

public sealed record AdminIdentityListResponse(
    IReadOnlyList<Guid> Items,
    string? NextCursor);

public sealed record AdminIdentityDetailResponse(
    Guid CommunityIdentityId,
    IReadOnlyList<AdminExternalIdentityResponse> ExternalIdentities,
    AdminEconomyResponse? Economy,
    AdminProgressionResponse? Progression,
    IReadOnlyList<AdminInventoryEntryResponse> Inventory,
    IReadOnlyList<AdminAchievementResponse> Achievements,
    AdminTitlesResponse? Titles,
    IReadOnlyList<AdminShopPurchaseResponse> ShopPurchases,
    IReadOnlyList<AdminNotificationResponse> Notifications,
    long? UnreadNotifications,
    IReadOnlyList<AdminAuditResponse> Audit);

public sealed record AdminExternalIdentityResponse(string Provider, string ExternalUserId);
public sealed record AdminEconomyResponse(long Balance);
public sealed record AdminProgressionResponse(long ExperiencePoints);
public sealed record AdminNotificationResponse(Guid Id, string Type, string Title, string? Message, bool IsRead, DateTimeOffset CreatedAtUtc);
public sealed record AdminAuditResponse(string Action, string TargetType, string TargetId, string Result, string RiskLevel, string? Reason, DateTimeOffset OccurredAtUtc);

public sealed record AdminActionRequest(Guid? RequestId, string? Reason);

public sealed record AdminProgressionGrantRequest(long? ExperiencePoints, string? Reason, Guid? RequestId);

public sealed record AdminInventoryAdjustmentRequest(Guid? ItemDefinitionId, long? Quantity, string? Reason, Guid? RequestId);

public sealed record AdminDefinitionRequest(string? DisplayName, string? Description, Guid? RequestId);

public sealed record AdminCommunityDefinitionActionRequest(Guid? DefinitionId, string? Reason, Guid? RequestId);

public sealed record AdminRewardDefinitionRequest(long? Amount, Guid? RequestId);

public sealed record AdminRewardPackageRequest(IReadOnlyList<Guid>? DefinitionIds, Guid? RequestId);

public sealed record AdminRewardGrantRequest(Guid? PackageId, string? Reason, Guid? RequestId);

public sealed record AdminInventoryEntryResponse(Guid ItemDefinitionId, long Quantity);

public sealed record AdminProgressionResponseV1(Guid CommunityIdentityId, long ExperiencePoints);

public sealed record AdminAchievementDefinitionResponse(Guid Id, string DisplayName, string? Description);

public sealed record AdminAchievementResponse(Guid DefinitionId, DateTimeOffset UnlockedAtUtc);

public sealed record AdminTitleDefinitionResponse(Guid Id, string DisplayName, string? Description);

public sealed record AdminTitlesResponse(Guid CommunityIdentityId, IReadOnlyList<Guid> UnlockedDefinitionIds, Guid? CurrentDefinitionId);

public sealed record AdminRewardDefinitionResponse(Guid Id, string Type, long Amount);

public sealed record AdminRewardPackageResponse(Guid Id, IReadOnlyList<Guid> DefinitionIds);

public sealed record AdminRewardGrantResponse(Guid Id, Guid CommunityIdentityId, Guid DefinitionId, string SourceType, string SourceId);

public sealed record AdminErrorResponse(string Error);
public sealed record AdminAlreadyCompletedResponse(bool AlreadyCompleted);
public sealed record AdminChangedResponse(bool Changed);
public sealed record AdminRewardGrantStatusResponse(bool Granted, bool AlreadyGranted);
public sealed record AdminShopPurchaseResponse(Guid Id, Guid ShopOfferId, Guid ItemDefinitionId, long PricePaid, DateTimeOffset PurchasedAtUtc);
