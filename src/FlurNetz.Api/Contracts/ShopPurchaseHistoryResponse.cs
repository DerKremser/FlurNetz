namespace FlurNetz.Api.Contracts;

/// <summary>
/// API-Antwort für eine keyset-paginierte Shop-Kaufhistorie.
/// </summary>
public sealed record ShopPurchaseHistoryResponse(
    IReadOnlyList<ShopPurchaseResponse> Items,
    string? NextCursor);
