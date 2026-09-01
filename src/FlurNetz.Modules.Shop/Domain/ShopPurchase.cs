using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Domain;

/// <summary>
/// Repräsentiert den unveränderlichen historischen Zustand eines erfolgreich abgeschlossenen Kaufs.
/// </summary>
public sealed class ShopPurchase
{
    private ShopPurchase(
        ShopPurchaseId id,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        ShopPrice pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        Id = id;
        ShopOfferId = shopOfferId;
        CommunityIdentityId = communityIdentityId;
        ItemDefinitionId = itemDefinitionId;
        PricePaid = pricePaid;
        PurchasedAtUtc = purchasedAtUtc;
    }

    public ShopPurchaseId Id { get; }

    public ShopOfferId ShopOfferId { get; }

    public CommunityIdentityId CommunityIdentityId { get; }

    public ItemDefinitionId ItemDefinitionId { get; }

    /// <summary>
    /// Liefert die gespeicherte Ziel-Item-ID als primitive Guid-Projektion für technische Adapter.
    /// </summary>
    public Guid ItemDefinitionIdValue => ItemDefinitionId.Value;

    public ShopPrice PricePaid { get; }

    public DateTimeOffset PurchasedAtUtc { get; }

    /// <summary>
    /// Erstellt einen neuen unveränderlichen Kauf-Snapshot.
    /// </summary>
    public static ShopPurchase Create(
        ShopPurchaseId id,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        ShopPrice pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        return CreateCore(
            id,
            shopOfferId,
            communityIdentityId,
            itemDefinitionId,
            pricePaid,
            purchasedAtUtc);
    }

    /// <summary>
    /// Rekonstruiert einen persistierten Kauf mit denselben Invarianten.
    /// </summary>
    public static ShopPurchase Rehydrate(
        ShopPurchaseId id,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        ShopPrice pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        return CreateCore(
            id,
            shopOfferId,
            communityIdentityId,
            itemDefinitionId,
            pricePaid,
            purchasedAtUtc);
    }

    private static ShopPurchase CreateCore(
        ShopPurchaseId id,
        ShopOfferId shopOfferId,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        ShopPrice pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        _ = ShopPurchaseId.Create(id.Value);
        _ = ShopOfferId.Create(shopOfferId.Value);
        _ = CommunityIdentityId.Create(communityIdentityId.Value);
        _ = ItemDefinitionId.Create(itemDefinitionId.Value);

        if (purchasedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Kaufzeitpunkt muss in UTC vorliegen.", nameof(purchasedAtUtc));
        }

        if (purchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Kaufzeitpunkt muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                nameof(purchasedAtUtc));
        }

        return new ShopPurchase(
            id,
            shopOfferId,
            communityIdentityId,
            itemDefinitionId,
            pricePaid,
            purchasedAtUtc);
    }
}
