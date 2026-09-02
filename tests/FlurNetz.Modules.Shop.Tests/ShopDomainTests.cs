using System.Reflection;
using System.Text;
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
    public void Create_CanonicalizesSetBoundariesToUtc()
    {
        var from = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));
        var until = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

        var window = AvailabilityWindow.Create(from, until);

        Assert.Equal(TimeSpan.Zero, window.AvailableFrom!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, window.AvailableUntil!.Value.Offset);
        Assert.Equal(from.UtcDateTime, window.AvailableFrom.Value.UtcDateTime);
        Assert.Equal(until.UtcDateTime, window.AvailableUntil.Value.UtcDateTime);
        Assert.Equal(
            AvailabilityWindow.Create(
                new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                until),
            window);
    }

    [Fact]
    public void Create_AcceptsMicrosecondPrecisionForBothBoundaries()
    {
        var from = From.AddTicks(10);
        var until = Until.AddTicks(10);

        var window = AvailabilityWindow.Create(from, until);

        Assert.Equal(from, window.AvailableFrom);
        Assert.Equal(until, window.AvailableUntil);
    }

    [Fact]
    public void Create_RejectsSubMicrosecondPrecision()
    {
        Assert.Throws<ArgumentException>(() => AvailabilityWindow.Create(From.AddTicks(1), null));
        Assert.Throws<ArgumentException>(() => AvailabilityWindow.Create(null, Until.AddTicks(1)));
    }

    [Fact]
    public void Create_RejectsAnEmptyWindowAfterUtcCanonicalization()
    {
        var from = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));
        var until = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => AvailabilityWindow.Create(from, until));
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
            2,
            7);

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
        Assert.Equal(7, offer.SortOrder);
    }

    [Fact]
    public void Create_StartsDisabled()
    {
        var offer = CreateOffer();

        Assert.False(offer.IsEnabled);
    }

    [Fact]
    public void Create_DefaultsSortOrderToZero()
    {
        var offer = CreateOffer();

        Assert.Equal(0, offer.SortOrder);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(5000)]
    public void Create_AcceptsZeroAndPositiveSortOrders(int sortOrder)
    {
        var offer = CreateOffer(sortOrder: sortOrder);

        Assert.Equal(sortOrder, offer.SortOrder);
    }

    [Fact]
    public void Create_RejectsNegativeSortOrder()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateOffer(sortOrder: -1));

        Assert.Equal("sortOrder", exception.ParamName);
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

        Assert.Equal(ShopOffer.MaxDisplayNameLength, CountUnicodeScalars(offer.DisplayName));
    }

    [Fact]
    public void Create_AcceptsDisplayNameAtMaximumUnicodeScalarLength()
    {
        var displayName = RepeatUnicodeScalar("😀", ShopOffer.MaxDisplayNameLength);

        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            displayName);

        Assert.Equal(displayName, offer.DisplayName);
        Assert.Equal(ShopOffer.MaxDisplayNameLength, CountUnicodeScalars(offer.DisplayName));
    }

    [Fact]
    public void Create_RejectsDisplayNameAboveMaximumUnicodeScalarLength()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            RepeatUnicodeScalar("😀", ShopOffer.MaxDisplayNameLength + 1)));
    }

    [Fact]
    public void Create_CountsMixedBmpAndSupplementaryUnicodeAsScalars()
    {
        var displayName = string.Concat(Enumerable.Repeat("a😀", ShopOffer.MaxDisplayNameLength / 2));

        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            displayName);

        Assert.Equal(ShopOffer.MaxDisplayNameLength, CountUnicodeScalars(offer.DisplayName));
        Assert.Equal(displayName, offer.DisplayName);
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

        Assert.Equal(ShopOffer.MaxDescriptionLength, CountUnicodeScalars(offer.Description!));
    }

    [Fact]
    public void Create_AcceptsDescriptionAtMaximumUnicodeScalarLength()
    {
        var description = RepeatUnicodeScalar("🧪", ShopOffer.MaxDescriptionLength);

        var offer = CreateOffer(description: description);

        Assert.Equal(description, offer.Description);
        Assert.Equal(ShopOffer.MaxDescriptionLength, CountUnicodeScalars(offer.Description!));
    }

    [Fact]
    public void Create_RejectsDescriptionAboveMaximumUnicodeScalarLength()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            RepeatUnicodeScalar("🧪", ShopOffer.MaxDescriptionLength + 1)));
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

    [Fact]
    public void Create_RejectsNullCharacterInDisplayName()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot\0intern"));
    }

    [Fact]
    public void Create_RejectsNullCharacterInDescription()
    {
        Assert.Throws<ArgumentException>(() => ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            "Beschreibung\0intern"));
    }

    [Fact]
    public void Create_RejectsUnpairedSurrogateInDisplayName()
    {
        foreach (var displayName in new[] { new string('\uD800', 1), new string('\uDC00', 1) })
        {
            Assert.Throws<ArgumentException>(() => ShopOffer.Create(
                ShopOfferId.New(),
                ItemDefinitionId.New(),
                displayName));
        }
    }

    [Fact]
    public void Create_RejectsUnpairedSurrogateInDescription()
    {
        foreach (var description in new[] { new string('\uD800', 1), new string('\uDC00', 1) })
        {
            Assert.Throws<ArgumentException>(() => ShopOffer.Create(
                ShopOfferId.New(),
                ItemDefinitionId.New(),
                "Angebot",
                description));
        }
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
    public void Rename_AcceptsMaximumSupplementaryUnicodeScalars()
    {
        var displayName = RepeatUnicodeScalar("😀", ShopOffer.MaxDisplayNameLength);
        var offer = CreateOffer();

        Assert.True(offer.Rename(displayName));
        Assert.Equal(displayName, offer.DisplayName);
        Assert.Equal(ShopOffer.MaxDisplayNameLength, CountUnicodeScalars(offer.DisplayName));
    }

    [Fact]
    public void Rename_RejectsNullCharacterAndUnpairedSurrogate()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.Rename("Neu\0intern"));
        Assert.Throws<ArgumentException>(() => offer.Rename(new string('\uD800', 1)));
        Assert.Equal("Angebot", offer.DisplayName);
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
    public void ChangeDescription_AcceptsMaximumSupplementaryUnicodeScalars()
    {
        var description = RepeatUnicodeScalar("🧪", ShopOffer.MaxDescriptionLength);
        var offer = CreateOffer();

        Assert.True(offer.ChangeDescription(description));
        Assert.Equal(description, offer.Description);
        Assert.Equal(ShopOffer.MaxDescriptionLength, CountUnicodeScalars(offer.Description!));
    }

    [Fact]
    public void ChangeDescription_RejectsNullCharacterAndUnpairedSurrogate()
    {
        var offer = CreateOffer();

        Assert.Throws<ArgumentException>(() => offer.ChangeDescription("Neu\0intern"));
        Assert.Throws<ArgumentException>(() => offer.ChangeDescription(new string('\uD800', 1)));
        Assert.Equal("Beschreibung", offer.Description);
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
    public void ChangeSortOrder_ChangesDifferentValueAndPreservesOtherState()
    {
        var offer = CreateOffer(sortOrder: 5);
        var id = offer.Id;
        var itemDefinitionId = offer.ItemDefinitionId;
        var displayName = offer.DisplayName;
        var description = offer.Description;
        var price = offer.Price;
        var availability = offer.Availability;
        var purchaseLimit = offer.PurchaseLimitPerIdentity;
        var isEnabled = offer.IsEnabled;

        Assert.True(offer.ChangeSortOrder(10));

        Assert.Equal(10, offer.SortOrder);
        Assert.Equal(id, offer.Id);
        Assert.Equal(itemDefinitionId, offer.ItemDefinitionId);
        Assert.Equal(displayName, offer.DisplayName);
        Assert.Equal(description, offer.Description);
        Assert.Equal(price, offer.Price);
        Assert.Equal(availability, offer.Availability);
        Assert.Equal(purchaseLimit, offer.PurchaseLimitPerIdentity);
        Assert.Equal(isEnabled, offer.IsEnabled);
    }

    [Fact]
    public void ChangeSortOrder_IsNoOpForTheSameValue()
    {
        var offer = CreateOffer(sortOrder: 5);

        Assert.False(offer.ChangeSortOrder(5));
        Assert.Equal(5, offer.SortOrder);
    }

    [Fact]
    public void ChangeSortOrder_RejectsNegativeValuesWithoutChangingState()
    {
        var offer = CreateOffer(sortOrder: 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => offer.ChangeSortOrder(-1));

        Assert.Equal(5, offer.SortOrder);
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
            nameof(ShopOffer.ChangeSortOrder),
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
        var sortOrder = typeof(ShopOffer).GetProperty(nameof(ShopOffer.SortOrder));

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
        Assert.NotNull(sortOrder);
        Assert.Null(sortOrder!.GetSetMethod());
    }

    [Fact]
    public void ShopOffer_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(ShopOffer).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    private static ShopOffer CreateOffer(
        string? description = "Beschreibung",
        int? purchaseLimitPerIdentity = 2,
        int sortOrder = 0) =>
        ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            description,
            ShopPrice.Create(25),
            AvailabilityWindow.Create(From, Until),
            purchaseLimitPerIdentity,
            sortOrder);

    private static string RepeatUnicodeScalar(string scalar, int count) =>
        string.Concat(Enumerable.Repeat(scalar, count));

    private static int CountUnicodeScalars(string value) => value.EnumerateRunes().Count();
}
