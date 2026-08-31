using System.Reflection;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Tests;

public sealed class ShopOfferIdTests
{
    [Fact]
    public void Create_AcceptsNonEmptyGuidAndPreservesValue()
    {
        var value = Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd");

        var id = ShopOfferId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => ShopOfferId.Create(Guid.Empty));
    }

    [Fact]
    public void New_CreatesAValidId()
    {
        Assert.NotEqual(Guid.Empty, ShopOfferId.New().Value);
    }

    [Fact]
    public void EqualValues_HaveValueSemantics()
    {
        var value = Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd");

        Assert.Equal(ShopOfferId.Create(value), ShopOfferId.Create(value));
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(ShopOfferId).GetProperty(nameof(ShopOfferId.Value));

        Assert.NotNull(property);
        Assert.Null(property!.GetSetMethod());
    }
}

public sealed class ShopPriceTests
{
    [Fact]
    public void Create_AllowsZero()
    {
        Assert.Equal(0, ShopPrice.Create(0).Value);
        Assert.Equal(ShopPrice.Zero, ShopPrice.Create(0));
    }

    [Fact]
    public void Create_AllowsPositiveValues()
    {
        Assert.Equal(42, ShopPrice.Create(42).Value);
    }

    [Fact]
    public void Create_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopPrice.Create(-1));
    }

    [Fact]
    public void EqualValues_HaveValueSemantics()
    {
        Assert.Equal(ShopPrice.Create(42), ShopPrice.Create(42));
        Assert.NotEqual(ShopPrice.Create(41), ShopPrice.Create(42));
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(ShopPrice).GetProperty(nameof(ShopPrice.Value));

        Assert.NotNull(property);
        Assert.Null(property!.GetSetMethod());
    }
}

public sealed class AvailabilityWindowTests
{
    private static readonly DateTimeOffset From =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = From.AddHours(2);

    [Fact]
    public void Create_AllowsAnUnboundedWindow()
    {
        var window = AvailabilityWindow.Create(null, null);

        Assert.Null(window.AvailableFrom);
        Assert.Null(window.AvailableUntil);
        Assert.True(window.IsAvailableAt(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void Create_AllowsOnlyAnInclusiveStart()
    {
        var window = AvailabilityWindow.Create(From, null);

        Assert.False(window.IsAvailableAt(From.AddTicks(-1)));
        Assert.True(window.IsAvailableAt(From));
        Assert.True(window.IsAvailableAt(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void Create_AllowsOnlyAnExclusiveEnd()
    {
        var window = AvailabilityWindow.Create(null, Until);

        Assert.True(window.IsAvailableAt(DateTimeOffset.MinValue));
        Assert.True(window.IsAvailableAt(Until.AddTicks(-1)));
        Assert.False(window.IsAvailableAt(Until));
    }

    [Fact]
    public void Create_AllowsAStartAndEnd()
    {
        var window = AvailabilityWindow.Create(From, Until);

        Assert.False(window.IsAvailableAt(From.AddTicks(-1)));
        Assert.True(window.IsAvailableAt(From));
        Assert.True(window.IsAvailableAt(Until.AddTicks(-1)));
        Assert.False(window.IsAvailableAt(Until));
    }

    [Fact]
    public void Create_UsesAbsoluteInstantsForDifferentOffsets()
    {
        var from = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));
        var until = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

        var window = AvailabilityWindow.Create(from, until);

        Assert.True(window.IsAvailableAt(
            new DateTimeOffset(2026, 1, 1, 7, 0, 0, TimeSpan.FromHours(-5))));
        Assert.True(window.IsAvailableAt(
            new DateTimeOffset(2026, 1, 1, 7, 59, 59, TimeSpan.FromHours(-5))));
        Assert.False(window.IsAvailableAt(
            new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void Create_RejectsEqualStartAndEnd()
    {
        Assert.Throws<ArgumentException>(() => AvailabilityWindow.Create(From, From));
    }

    [Fact]
    public void Create_RejectsStartAfterEnd()
    {
        Assert.Throws<ArgumentException>(() => AvailabilityWindow.Create(Until, From));
    }

    [Fact]
    public void Contains_UsesTheSameHalfOpenSemantics()
    {
        var window = AvailabilityWindow.Create(From, Until);

        Assert.True(window.Contains(From));
        Assert.False(window.Contains(Until));
    }
}

public sealed class ShopOfferTests
{
    private static readonly DateTimeOffset From =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = From.AddHours(2);

    [Fact]
    public void Create_PreservesTargetAndCanonicalizesPresentation()
    {
        var id = ShopOfferId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var offer = ShopOffer.Create(
            id,
            itemDefinitionId,
            "  Angebot  ",
            "  Beschreibung  ",
            ShopPrice.Create(25),
            AvailabilityWindow.Create(From, Until),
            2);

        Assert.Equal(id, offer.Id);
        Assert.Equal(id, offer.ShopOfferId);
        Assert.Equal(itemDefinitionId, offer.ItemDefinitionId);
        Assert.Equal("Angebot", offer.DisplayName);
        Assert.Equal("Beschreibung", offer.Description);
        Assert.Equal(ShopPrice.Create(25), offer.Price);
        Assert.Equal(offer.Price, offer.ShopPrice);
        Assert.Equal(AvailabilityWindow.Create(From, Until), offer.Availability);
        Assert.Equal(offer.Availability, offer.AvailabilityWindow);
        Assert.Equal(2, offer.PurchaseLimitPerIdentity);
    }

    [Fact]
    public void Create_StartsDisabled()
    {
        var offer = CreateOffer();

        Assert.False(offer.IsEnabled);
    }

    [Fact]
    public void Create_AllowsFreeOffersAndUnboundedAvailabilityAndLimit()
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Kostenlos",
            null,
            ShopPrice.Zero,
            AvailabilityWindow.Create(null, null),
            null);

        Assert.Equal(ShopPrice.Zero, offer.Price);
        Assert.Null(offer.PurchaseLimitPerIdentity);
        Assert.True(offer.IsAvailableAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsInvalidTargetIds()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            default,
            ItemDefinitionId.New(),
            "Angebot"));
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            default,
            "Angebot"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsInvalidDisplayNames(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            displayName!));
    }

    [Fact]
    public void Create_AcceptsDisplayNameAtMaximumLength()
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            new string('a', ShopOffer.MaxDisplayNameLength));

        Assert.Equal(ShopOffer.MaxDisplayNameLength, offer.DisplayName.Length);
    }

    [Fact]
    public void Create_RejectsDisplayNameAboveMaximumLength()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            new string('a', ShopOffer.MaxDisplayNameLength + 1)));
    }

    [Fact]
    public void Create_TrimsUnicodeWhitespaceBeforeApplyingDisplayNameLimit()
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "\u2003" + new string('a', ShopOffer.MaxDisplayNameLength) + "\u3000");

        Assert.Equal(new string('a', ShopOffer.MaxDisplayNameLength), offer.DisplayName);
    }

    [Fact]
    public void Create_RejectsDisplayNameThatRemainsTooLongAfterUnicodeTrim()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "\u2003" + new string('a', ShopOffer.MaxDisplayNameLength + 1) + "\u3000"));
    }

    [Fact]
    public void Create_RejectsUnicodeWhitespaceOnlyDisplayName()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "\u2003\u00a0\u202f\u3000"));
    }

    [Fact]
    public void Create_AllowsNullDescription()
    {
        var offer = CreateOffer(description: null);

        Assert.Null(offer.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyOrWhitespaceDescription(string description)
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            description));
    }

    [Fact]
    public void Create_AcceptsDescriptionAtMaximumLength()
    {
        var offer = CreateOffer(description: new string('b', ShopOffer.MaxDescriptionLength));

        Assert.Equal(ShopOffer.MaxDescriptionLength, offer.Description!.Length);
    }

    [Fact]
    public void Create_RejectsDescriptionAboveMaximumLength()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            new string('b', ShopOffer.MaxDescriptionLength + 1)));
    }

    [Fact]
    public void Create_TrimsUnicodeWhitespaceBeforeApplyingDescriptionLimit()
    {
        var offer = CreateOffer(
            description: "\u2003" + new string('b', ShopOffer.MaxDescriptionLength) + "\u3000");

        Assert.Equal(new string('b', ShopOffer.MaxDescriptionLength), offer.Description);
    }

    [Fact]
    public void Create_RejectsDescriptionThatRemainsTooLongAfterUnicodeTrim()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            "\u2003" + new string('b', ShopOffer.MaxDescriptionLength + 1) + "\u3000"));
    }

    [Fact]
    public void Create_RejectsUnicodeWhitespaceOnlyDescription()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            "\u2003\u00a0\u202f\u3000"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Create_AcceptsPositivePurchaseLimits(int limit)
    {
        var offer = CreateOffer(purchaseLimitPerIdentity: limit);

        Assert.Equal(limit, offer.PurchaseLimitPerIdentity);
    }

    [Fact]
    public void Create_AllowsAnUnlimitedPurchaseLimit()
    {
        Assert.Null(CreateOffer(purchaseLimitPerIdentity: null).PurchaseLimitPerIdentity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositivePurchaseLimits(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateOffer(purchaseLimitPerIdentity: limit));
    }

    [Fact]
    public void Rename_ChangesDisplayNameAndPreservesOtherState()
    {
        var offer = CreateOffer();

        var changed = offer.Rename("  Neues Angebot  ");

        Assert.True(changed);
        Assert.Equal("Neues Angebot", offer.DisplayName);
        Assert.Equal("Beschreibung", offer.Description);
        Assert.Equal(ShopPrice.Create(25), offer.Price);
    }

    [Fact]
    public void Rename_IsIdempotentForTheSameCanonicalName()
    {
        var offer = CreateOffer();

        Assert.False(offer.Rename("  Angebot  "));
        Assert.Equal("Angebot", offer.DisplayName);
    }

    [Fact]
    public void Rename_TrimsUnicodeWhitespaceBeforeApplyingDisplayNameLimit()
    {
        var offer = CreateOffer();

        Assert.True(offer.Rename(
            "\u2003" + new string('n', ShopOffer.MaxDisplayNameLength) + "\u3000"));
        Assert.Equal(new string('n', ShopOffer.MaxDisplayNameLength), offer.DisplayName);
    }

    [Fact]
    public void Rename_RejectsDisplayNameThatRemainsTooLongAfterUnicodeTrim()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.Rename(
            "\u2003" + new string('n', ShopOffer.MaxDisplayNameLength + 1) + "\u3000"));
    }

    [Fact]
    public void ChangeDisplayName_DelegatesToRename()
    {
        var offer = CreateOffer();

        Assert.True(offer.ChangeDisplayName("Neu"));
        Assert.Equal("Neu", offer.DisplayName);
    }

    [Fact]
    public void ChangeDisplayName_RejectsUnicodeWhitespaceOnlyName()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.ChangeDisplayName("\u2003\u00a0\u202f\u3000"));
        Assert.Equal("Angebot", offer.DisplayName);
    }

    [Fact]
    public void ChangeDescription_SetsAndRemovesDescription()
    {
        var offer = CreateOffer();

        Assert.True(offer.ChangeDescription("  Neu  "));
        Assert.Equal("Neu", offer.Description);
        Assert.True(offer.ChangeDescription(null));
        Assert.Null(offer.Description);
        Assert.False(offer.ChangeDescription(null));
    }

    [Fact]
    public void ChangeDescription_TrimsUnicodeWhitespaceBeforeApplyingDescriptionLimit()
    {
        var offer = CreateOffer();

        Assert.True(offer.ChangeDescription(
            "\u2003" + new string('d', ShopOffer.MaxDescriptionLength) + "\u3000"));
        Assert.Equal(new string('d', ShopOffer.MaxDescriptionLength), offer.Description);
    }

    [Fact]
    public void ChangeDescription_RejectsDescriptionThatRemainsTooLongAfterUnicodeTrim()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.ChangeDescription(
            "\u2003" + new string('d', ShopOffer.MaxDescriptionLength + 1) + "\u3000"));
        Assert.Equal("Beschreibung", offer.Description);
    }

    [Fact]
    public void ChangeDescription_RejectsUnicodeWhitespaceOnlyWithoutChangingState()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.ChangeDescription("\u2003\u00a0\u202f\u3000"));
        Assert.Equal("Beschreibung", offer.Description);
    }

    [Fact]
    public void ChangePrice_ChangesPriceAndIsIdempotent()
    {
        var offer = CreateOffer();

        Assert.True(offer.ChangePrice(ShopPrice.Create(50)));
        Assert.Equal(ShopPrice.Create(50), offer.Price);
        Assert.False(offer.ChangePrice(50));
    }

    [Fact]
    public void ChangePrice_RejectsNegativeValuesWithoutChangingState()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentOutOfRangeException>(() => offer.ChangePrice(-1));

        Assert.Equal(ShopPrice.Create(25), offer.Price);
    }

    [Fact]
    public void ChangeAvailability_ChangesWindowAndIsIdempotent()
    {
        var offer = CreateOffer();
        var newWindow = AvailabilityWindow.Create(From.AddDays(1), Until.AddDays(1));

        Assert.True(offer.ChangeAvailability(newWindow));
        Assert.Equal(newWindow, offer.Availability);
        Assert.False(offer.ChangeAvailability(newWindow));
    }

    [Fact]
    public void ChangePurchaseLimit_ChangesAndRemovesLimit()
    {
        var offer = CreateOffer(purchaseLimitPerIdentity: 1);

        Assert.True(offer.ChangePurchaseLimit(2));
        Assert.Equal(2, offer.PurchaseLimitPerIdentity);
        Assert.True(offer.ChangePurchaseLimit(null));
        Assert.Null(offer.PurchaseLimitPerIdentity);
        Assert.False(offer.ChangePurchaseLimit(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChangePurchaseLimit_RejectsNonPositiveValuesWithoutChangingState(int limit)
    {
        var offer = CreateOffer(purchaseLimitPerIdentity: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => offer.ChangePurchaseLimit(limit));

        Assert.Equal(1, offer.PurchaseLimitPerIdentity);
    }

    [Fact]
    public void EnableAndDisableChangeStateIdempotently()
    {
        var offer = CreateOffer();

        Assert.True(offer.Enable());
        Assert.True(offer.IsEnabled);
        Assert.False(offer.Enable());
        Assert.True(offer.Disable());
        Assert.False(offer.IsEnabled);
        Assert.False(offer.Disable());
    }

    [Fact]
    public void TargetIdentifiersAreImmutable()
    {
        var id = typeof(ShopOffer).GetProperty(nameof(ShopOffer.Id));
        var shopOfferId = typeof(ShopOffer).GetProperty(nameof(ShopOffer.ShopOfferId));
        var itemDefinitionId = typeof(ShopOffer).GetProperty(nameof(ShopOffer.ItemDefinitionId));

        Assert.NotNull(id);
        Assert.Null(id!.GetSetMethod());
        Assert.NotNull(shopOfferId);
        Assert.Null(shopOfferId!.GetSetMethod());
        Assert.NotNull(itemDefinitionId);
        Assert.Null(itemDefinitionId!.GetSetMethod());

        var allowedPublicInstanceMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(ShopOffer.IsAvailableAt),
            nameof(ShopOffer.Rename),
            nameof(ShopOffer.ChangeDisplayName),
            nameof(ShopOffer.ChangeDescription),
            nameof(ShopOffer.ChangePrice),
            nameof(ShopOffer.ChangeAvailability),
            nameof(ShopOffer.ChangePurchaseLimit),
            nameof(ShopOffer.Enable),
            nameof(ShopOffer.Disable)
        };

        var unexpectedPublicInstanceMethods = typeof(ShopOffer)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => !allowedPublicInstanceMethodNames.Contains(method.Name))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unexpectedPublicInstanceMethods);

        var targetTypes = new[] { typeof(ShopOfferId), typeof(ItemDefinitionId) };
        var publicMethodsWithTargetParameters = typeof(ShopOffer)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetParameters().Any(parameter =>
                targetTypes.Contains(parameter.ParameterType)))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(publicMethodsWithTargetParameters);
    }

    [Fact]
    public void PublicStatePropertiesCannotBeSetExternally()
    {
        var displayName = typeof(ShopOffer).GetProperty(nameof(ShopOffer.DisplayName));
        var description = typeof(ShopOffer).GetProperty(nameof(ShopOffer.Description));
        var price = typeof(ShopOffer).GetProperty(nameof(ShopOffer.Price));
        var isEnabled = typeof(ShopOffer).GetProperty(nameof(ShopOffer.IsEnabled));
        var availability = typeof(ShopOffer).GetProperty(nameof(ShopOffer.Availability));
        var purchaseLimit = typeof(ShopOffer).GetProperty(nameof(ShopOffer.PurchaseLimitPerIdentity));

        Assert.NotNull(displayName);
        Assert.Null(displayName!.GetSetMethod());
        Assert.NotNull(description);
        Assert.Null(description!.GetSetMethod());
        Assert.NotNull(price);
        Assert.Null(price!.GetSetMethod());
        Assert.NotNull(isEnabled);
        Assert.Null(isEnabled!.GetSetMethod());
        Assert.NotNull(availability);
        Assert.Null(availability!.GetSetMethod());
        Assert.NotNull(purchaseLimit);
        Assert.Null(purchaseLimit!.GetSetMethod());
    }

    [Fact]
    public void ShopOffer_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(ShopOffer).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    private static ShopOffer CreateOffer(
        string? description = "Beschreibung",
        int? purchaseLimitPerIdentity = 2) =>
        ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            description,
            ShopPrice.Create(25),
            AvailabilityWindow.Create(From, Until),
            purchaseLimitPerIdentity);
}
