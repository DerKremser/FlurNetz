using System.Text.Json.Serialization;

namespace FlurNetz.Api.Contracts;

/// <summary>API-Request für Create und Replace einer Automation-Rule.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AutomationRuleRequest(
    string? DisplayName,
    string? Description,
    string? TriggerType,
    IReadOnlyList<AutomationConditionRequest>? Conditions,
    IReadOnlyList<AutomationActionRequest>? Actions,
    int? SortOrder);

/// <summary>API-Request für eine bekannte V1-Condition.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AutomationConditionRequest(
    string? Type,
    Guid? CommunityIdentityId,
    Guid? ShopOfferId,
    Guid? ItemDefinitionId,
    long? Amount);

/// <summary>API-Request für eine bekannte V1-Action.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AutomationActionRequest(
    string? Type,
    long? Amount,
    string? Title,
    string? Message,
    Guid? OverlayChannelId = null,
    string? Variant = null,
    int? DurationMilliseconds = null);

/// <summary>API-Darstellung einer Automation-Condition.</summary>
public sealed record AutomationConditionResponse(
    int Position,
    string Type,
    Guid? CommunityIdentityId,
    Guid? ShopOfferId,
    Guid? ItemDefinitionId,
    long? Amount);

/// <summary>API-Darstellung einer Automation-Action.</summary>
public sealed record AutomationActionResponse(
    int Position,
    string Type,
    long? Amount,
    string? Title,
    string? Message,
    Guid? OverlayChannelId = null,
    string? Variant = null,
    int? DurationMilliseconds = null);

/// <summary>API-Darstellung einer Automation-Rule.</summary>
public sealed record AutomationRuleResponse(
    Guid Id,
    string DisplayName,
    string? Description,
    string TriggerType,
    IReadOnlyList<AutomationConditionResponse> Conditions,
    IReadOnlyList<AutomationActionResponse> Actions,
    int SortOrder,
    bool IsEnabled,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>API-Darstellung der vollständigen Rule-Liste.</summary>
public sealed record AutomationRuleListResponse(IReadOnlyList<AutomationRuleResponse> Items);

/// <summary>API-Darstellung einer Execution-History-Zeile.</summary>
public sealed record AutomationExecutionResponse(
    Guid Id,
    Guid AutomationRuleId,
    Guid TriggerMessageId,
    string TriggerMessageType,
    int TriggerSchemaVersion,
    Guid CommunityIdentityId,
    DateTimeOffset TriggerOccurredAtUtc,
    DateTimeOffset ExecutedAtUtc);

/// <summary>API-Darstellung einer paginierten Execution-History.</summary>
public sealed record AutomationExecutionPageResponse(
    IReadOnlyList<AutomationExecutionResponse> Items,
    string? NextCursor);
