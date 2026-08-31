using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Signalisiert die Wiederverwendung einer Request-ID für einen anderen fachlichen Kauf.
/// </summary>
public sealed class ShopPurchaseIdempotencyConflictException : InvalidOperationException
{
    public ShopPurchaseIdempotencyConflictException(ShopPurchaseRequestId requestId)
        : base($"Die Shop-Purchase-Request-ID '{ShopPurchaseRequestId.Create(requestId.Value).Value}' wurde bereits für einen anderen Kauf verwendet.")
    {
        RequestId = ShopPurchaseRequestId.Create(requestId.Value);
    }

    public ShopPurchaseRequestId RequestId { get; }
}
