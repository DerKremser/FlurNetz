namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort mit öffentlich sichtbaren Shop-Angeboten.
/// </summary>
public sealed record ShopOfferListResponse(IReadOnlyList<ShopOfferResponse> Items);
