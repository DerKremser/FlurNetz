using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopOfferRehydrationTests
{
    private static readonly DateTimeOffset From =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

    private static readonly DateTimeOffset Until =
        new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RehydrateRestoresEveryPersistedField()
    {
        var id = ShopOfferId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        var availability = AvailabilityWindow.Create(From, Until);

        var offer = ShopOffer.Rehydrate(
            id,
            itemDefinitionId,
            "Angebot",
            "Beschreibung",
            ShopPrice.Create(42),
            true,
            availability,
            3);

        Assert.Equal(id, offer.Id);
        Assert.Equal(itemDefinitionId, offer.ItemDefinitionId);
        Assert.Equal("Angebot", offer.DisplayName);
        Assert.Equal("Beschreibung", offer.Description);
        Assert.Equal(ShopPrice.Create(42), offer.Price);
        Assert.True(offer.IsEnabled);
        Assert.Equal(availability, offer.Availability);
        Assert.Equal(3, offer.PurchaseLimitPerIdentity);
    }

    [Fact]
    public void RehydratePreservesDisabledState()
    {
        var offer = ShopOffer.Rehydrate(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            null,
            ShopPrice.Zero,
            false,
            AvailabilityWindow.Create(null, null),
            null);

        Assert.False(offer.IsEnabled);
    }

    [Fact]
    public void RehydrateCanonicalizesValidTextUsingDomainRules()
    {
        var offer = ShopOffer.Rehydrate(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "  Angebot  ",
            "  Beschreibung  ",
            ShopPrice.Zero,
            true,
            AvailabilityWindow.Create(null, null),
            1);

        Assert.Equal("Angebot", offer.DisplayName);
        Assert.Equal("Beschreibung", offer.Description);
    }

    [Fact]
    public void RehydrateRejectsInvalidValues()
    {
        var id = ShopOfferId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        var validPrice = ShopPrice.Zero;
        var validAvailability = AvailabilityWindow.Create(null, null);

        Assert.Throws<ArgumentException>(() => ShopOffer.Rehydrate(
            default, itemDefinitionId, "Angebot", null, validPrice, false, validAvailability, null));
        Assert.Throws<ArgumentException>(() => ShopOffer.Rehydrate(
            id, default, "Angebot", null, validPrice, false, validAvailability, null));
        Assert.Throws<ArgumentException>(() => ShopOffer.Rehydrate(
            id, itemDefinitionId, "   ", null, validPrice, false, validAvailability, null));
        Assert.Throws<ArgumentException>(() => ShopOffer.Rehydrate(
            id, itemDefinitionId, "Angebot", "   ", validPrice, false, validAvailability, null));
        Assert.Throws<ArgumentException>(() => ShopOffer.Rehydrate(
            id,
            itemDefinitionId,
            "Angebot",
            null,
            validPrice,
            false,
            AvailabilityWindow.Create(From, From),
            null));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopOffer.Rehydrate(
            id, itemDefinitionId, "Angebot", null, validPrice, false, validAvailability, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopOffer.Rehydrate(
            id, itemDefinitionId, "Angebot", null, validPrice, false, validAvailability, -1));
    }

    [Fact]
    public void RehydrateDoesNotExposeTargetMutation()
    {
        var offer = ShopOffer.Rehydrate(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            null,
            ShopPrice.Zero,
            true,
            AvailabilityWindow.Create(null, null),
            null);

        Assert.Null(typeof(ShopOffer).GetProperty(nameof(ShopOffer.Id))!.GetSetMethod());
        Assert.Null(typeof(ShopOffer).GetProperty(nameof(ShopOffer.ItemDefinitionId))!.GetSetMethod());
        Assert.Equal(ShopOfferId.Create(offer.Id.Value), offer.Id);
    }
}
