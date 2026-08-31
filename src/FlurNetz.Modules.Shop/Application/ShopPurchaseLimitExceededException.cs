using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Signalisiert, dass das Kauflimit eines Angebots für eine Identität bereits erreicht ist.
/// </summary>
public sealed class ShopPurchaseLimitExceededException : InvalidOperationException
{
    public ShopPurchaseLimitExceededException(
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        int purchaseLimit)
        : base($"Das Kauflimit {purchaseLimit} für Angebot '{ShopOfferId.Create(shopOfferId.Value).Value}' und Identität '{CommunityIdentityId.Create(communityIdentityId.Value).Value}' ist erreicht.")
    {
        if (purchaseLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(purchaseLimit));
        }

        ShopOfferId = ShopOfferId.Create(shopOfferId.Value);
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        PurchaseLimit = purchaseLimit;
    }

    public ShopOfferId ShopOfferId { get; }

    public CommunityIdentityId CommunityIdentityId { get; }

    public int PurchaseLimit { get; }
}
