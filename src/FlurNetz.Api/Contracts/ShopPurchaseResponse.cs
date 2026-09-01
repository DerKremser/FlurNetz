namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Darstellung eines abgeschlossenen Shop-Kaufs mit seinem historischen Snapshot.
/// </summary>
public sealed record ShopPurchaseResponse(
    Guid Id,
    Guid ShopOfferId,
    Guid CommunityIdentityId,
    Guid ItemDefinitionId,
    long PricePaid,
    DateTimeOffset PurchasedAtUtc);
