namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-eigener Request-Vertrag für den Kauf eines einzelnen Shop-Angebots.
/// </summary>
public sealed record PurchaseShopOfferRequest(
    Guid RequestId,
    Guid CommunityIdentityId);
