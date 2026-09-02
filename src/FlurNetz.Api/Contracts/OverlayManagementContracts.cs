using System.Text.Json.Serialization;

namespace FlurNetz.Api.Contracts;

/// <summary>API-Request für einen Overlay-Kanal.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OverlayChannelRequest(string? DisplayName, string? Description);

/// <summary>API-Darstellung eines Overlay-Kanals ohne geheime Werte.</summary>
public sealed record OverlayChannelResponse(
    Guid Id,
    string DisplayName,
    string? Description,
    bool IsEnabled,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>API-Darstellung einer Kanal-Liste.</summary>
public sealed record OverlayChannelListResponse(IReadOnlyList<OverlayChannelResponse> Items);

/// <summary>API-Darstellung einer einmaligen Source-Key-Ausgabe.</summary>
public sealed record OverlayChannelSecretResponse(
    OverlayChannelResponse Channel,
    string SourceKey,
    string BrowserSourceUrl);

/// <summary>API-Request für einen manuellen Preview-Alert.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OverlayAlertRequest(
    string? Title,
    string? Message,
    string? Variant,
    int? DurationMilliseconds,
    string? SourceType,
    string? SourceId);

/// <summary>API-Darstellung eines Publish-Ergebnisses.</summary>
public sealed record OverlayAlertPublishResponse(string Status, Guid? AlertId);
