namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-eigener Request-Vertrag zum Anlegen eines Shop-Angebots.
/// </summary>
public sealed record CreateShopOfferRequest(
    Guid ItemDefinitionId,
    string? DisplayName,
    string? Description,
    long? Price,
    DateTimeOffset? AvailableFromUtc,
    DateTimeOffset? AvailableUntilUtc,
    int? PurchaseLimitPerIdentity,
    int? SortOrder = null);
