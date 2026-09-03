namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-eigener Request-Vertrag zum Umbenennen eines Shop-Angebots.
/// </summary>
public sealed record RenameShopOfferRequest(string? DisplayName, Guid? RequestId = null);

/// <summary>
/// API-eigener Request-Vertrag zum Setzen oder Entfernen einer Shop-Angebotsbeschreibung.
/// </summary>
public sealed record ChangeShopOfferDescriptionRequest(string? Description, Guid? RequestId = null);

/// <summary>
/// API-eigener Request-Vertrag zum Ändern des Shop-Angebotspreises.
/// </summary>
public sealed record ChangeShopOfferPriceRequest(long? Price, Guid? RequestId = null);

/// <summary>
/// API-eigener Request-Vertrag zum Ändern des Availability-Fensters eines Shop-Angebots.
/// </summary>
public sealed record ChangeShopOfferAvailabilityRequest(
    DateTimeOffset? AvailableFromUtc,
    DateTimeOffset? AvailableUntilUtc,
    Guid? RequestId = null);

/// <summary>
/// API-eigener Request-Vertrag zum Setzen oder Entfernen des Kauflimits.
/// </summary>
public sealed record ChangeShopOfferPurchaseLimitRequest(int? PurchaseLimitPerIdentity, Guid? RequestId = null);

/// <summary>
/// API-eigener Request-Vertrag zum Ändern der fachlichen Reihenfolge eines Shop-Angebots.
/// </summary>
public sealed record ChangeShopOfferSortOrderRequest(int? SortOrder, Guid? RequestId = null);
