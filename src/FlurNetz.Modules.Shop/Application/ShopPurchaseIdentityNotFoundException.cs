using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Signalisiert, dass ein Kauf für eine nicht vorhandene interne Identität angefordert wurde.
/// </summary>
public sealed class ShopPurchaseIdentityNotFoundException : InvalidOperationException
{
    public ShopPurchaseIdentityNotFoundException(CommunityIdentityId communityIdentityId)
        : base($"Die Community-Identität '{CommunityIdentityId.Create(communityIdentityId.Value).Value}' wurde für den Shop-Kauf nicht gefunden.")
    {
        CommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
    }

    public CommunityIdentityId CommunityIdentityId { get; }
}
