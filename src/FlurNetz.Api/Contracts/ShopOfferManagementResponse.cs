namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Darstellung eines Shop-Angebots für die interne Katalogverwaltung.
/// </summary>
public sealed record ShopOfferManagementResponse(
    Guid Id,
    Guid ItemDefinitionId,
    string DisplayName,
    string? Description,
    long Price,
    bool IsEnabled,
    bool IsArchived,
    DateTimeOffset? AvailableFromUtc,
    DateTimeOffset? AvailableUntilUtc,
    int? PurchaseLimitPerIdentity,
    int SortOrder = 0);
