namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort mit dem vollständigen internen Shop-Angebotskatalog.
/// </summary>
public sealed record ShopOfferManagementListResponse(
    IReadOnlyList<ShopOfferManagementResponse> Items);
