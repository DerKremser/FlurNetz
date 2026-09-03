using System.Text.Json.Serialization;

namespace FlurNetz.Api.Contracts;

/// <summary>API-Request zum Verknüpfen einer externen Identität.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExternalIdentityMappingRequest(
    string? Provider,
    string? ExternalUserId,
    Guid? CommunityIdentityId,
    string? Reason = null,
    Guid? RequestId = null);

/// <summary>API-Darstellung einer externen Identitätsverknüpfung.</summary>
public sealed record ExternalIdentityMappingResponse(
    string Provider,
    string ExternalUserId,
    Guid CommunityIdentityId);

/// <summary>API-Darstellung einer Mapping-Liste.</summary>
public sealed record ExternalIdentityMappingListResponse(
    IReadOnlyList<ExternalIdentityMappingResponse> Items);
