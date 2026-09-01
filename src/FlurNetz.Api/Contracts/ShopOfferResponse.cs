namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Darstellung eines öffentlich sichtbaren Shop-Angebots.
/// </summary>
public sealed record ShopOfferResponse(
    Guid Id,
    Guid ItemDefinitionId,
    string DisplayName,
    string? Description,
    long Price,
    DateTimeOffset? AvailableFromUtc,
    DateTimeOffset? AvailableUntilUtc,
    int? PurchaseLimitPerIdentity);
