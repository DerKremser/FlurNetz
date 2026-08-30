using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Domain;
using System.Reflection;

namespace FlurNetz.Modules.Inventory.Tests;

public sealed class ItemDefinitionIdTests
{
    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = ItemDefinitionId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("598be2de-e676-4de4-8338-e4138e03640e");

        var id = ItemDefinitionId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => ItemDefinitionId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("598be2de-e676-4de4-8338-e4138e03640e");

        Assert.Equal(ItemDefinitionId.Create(value), ItemDefinitionId.Create(value));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = ItemDefinitionId.Create(Guid.Parse("598be2de-e676-4de4-8338-e4138e03640e"));
        var second = ItemDefinitionId.Create(Guid.Parse("155699fe-3748-4974-83d4-e9f6c4f0f7a2"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(ItemDefinitionId).GetProperty(nameof(ItemDefinitionId.Value));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }
}

public sealed class InventoryQuantityTests
{
    [Fact]
    public void Zero_IsValid()
    {
        Assert.Equal(0, InventoryQuantity.Zero.Value);
        Assert.Equal(InventoryQuantity.Zero, InventoryQuantity.Create(0));
    }

    [Fact]
    public void Create_AcceptsPositiveValues()
    {
        var quantity = InventoryQuantity.Create(42);

        Assert.Equal(42, quantity.Value);
    }

    [Fact]
    public void Create_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryQuantity.Create(-1));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        Assert.Equal(InventoryQuantity.Create(42), InventoryQuantity.Create(42));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(InventoryQuantity.Create(41), InventoryQuantity.Create(42));
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(InventoryQuantity).GetProperty(nameof(InventoryQuantity.Value));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Add_IncreasesPositiveAmountWithoutMutatingTheOriginal()
    {
        var original = InventoryQuantity.Create(10);

        var result = original.Add(5);

        Assert.Equal(15, result.Value);
        Assert.Equal(10, original.Value);
    }

    [Fact]
    public void Add_AcceptsLongMaxValueFromZero()
    {
        var result = InventoryQuantity.Zero.Add(long.MaxValue);

        Assert.Equal(long.MaxValue, result.Value);
    }

    [Fact]
    public void Add_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryQuantity.Zero.Add(0));
    }

    [Fact]
    public void Add_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryQuantity.Zero.Add(-1));
    }

    [Fact]
    public void Add_RejectsOverflow()
    {
        var quantity = InventoryQuantity.Create(long.MaxValue);

        Assert.Throws<OverflowException>(() => quantity.Add(1));
    }

    [Fact]
    public void Remove_DecreasesPositiveAmountWithoutMutatingTheOriginal()
    {
        var original = InventoryQuantity.Create(10);

        var result = original.Remove(4);

        Assert.Equal(6, result.Value);
        Assert.Equal(10, original.Value);
    }

    [Fact]
    public void Remove_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryQuantity.Zero.Remove(0));
    }

    [Fact]
    public void Remove_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryQuantity.Zero.Remove(-1));
    }

    [Fact]
    public void Remove_AllowsReducingTheQuantityExactlyToZero()
    {
        var result = InventoryQuantity.Create(5).Remove(5);

        Assert.Equal(InventoryQuantity.Zero, result);
    }

    [Fact]
    public void Remove_RejectsAnInsufficientQuantity()
    {
        var quantity = InventoryQuantity.Create(5);

        Assert.Throws<InsufficientInventoryQuantityException>(() => quantity.Remove(6));
    }

    [Fact]
    public void FailedRemove_DoesNotChangeTheOriginalImmutableValue()
    {
        var original = InventoryQuantity.Create(5);

        Assert.Throws<InsufficientInventoryQuantityException>(() => original.Remove(6));

        Assert.Equal(5, original.Value);
    }
}

public sealed class CommunityInventoryEntryTests
{
    [Fact]
    public void Create_CarriesTheProvidedIdentifiers()
    {
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();

        var entry = CommunityInventoryEntry.Create(communityIdentityId, itemDefinitionId);

        Assert.Equal(communityIdentityId, entry.CommunityIdentityId);
        Assert.Equal(itemDefinitionId, entry.ItemDefinitionId);
    }

    [Fact]
    public void Create_StartsWithZeroQuantity()
    {
        var entry = CreateEntry();

        Assert.Equal(InventoryQuantity.Zero, entry.Quantity);
    }

    [Fact]
    public void Add_IncreasesTheQuantity()
    {
        var entry = CreateEntry();

        entry.Add(25);

        Assert.Equal(25, entry.Quantity.Value);
    }

    [Fact]
    public void Add_AccumulatesSeveralAdditions()
    {
        var entry = CreateEntry();

        entry.Add(10);
        entry.Add(7);

        Assert.Equal(17, entry.Quantity.Value);
    }

    [Fact]
    public void Remove_ReducesTheQuantity()
    {
        var entry = CreateEntry();
        entry.Add(10);

        entry.Remove(4);

        Assert.Equal(6, entry.Quantity.Value);
    }

    [Fact]
    public void AddAndRemove_CanBeCombined()
    {
        var entry = CreateEntry();

        entry.Add(20);
        entry.Remove(8);
        entry.Add(3);

        Assert.Equal(15, entry.Quantity.Value);
    }

    [Fact]
    public void Remove_CanReduceTheQuantityExactlyToZero()
    {
        var entry = CreateEntry();
        entry.Add(5);

        entry.Remove(5);

        Assert.Equal(InventoryQuantity.Zero, entry.Quantity);
    }

    [Fact]
    public void Remove_RejectsUnderflowWithoutChangingTheQuantity()
    {
        var entry = CreateEntry();
        entry.Add(5);

        Assert.Throws<InsufficientInventoryQuantityException>(() => entry.Remove(6));
        Assert.Equal(5, entry.Quantity.Value);
    }

    [Fact]
    public void Add_RejectsOverflowWithoutChangingTheQuantity()
    {
        var entry = CreateEntry();
        entry.Add(long.MaxValue);

        Assert.Throws<OverflowException>(() => entry.Add(1));
        Assert.Equal(long.MaxValue, entry.Quantity.Value);
    }

    [Fact]
    public void CommunityIdentityId_IsImmutable()
    {
        var property = typeof(CommunityInventoryEntry).GetProperty(
            nameof(CommunityInventoryEntry.CommunityIdentityId));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void ItemDefinitionId_IsImmutable()
    {
        var property = typeof(CommunityInventoryEntry).GetProperty(
            nameof(CommunityInventoryEntry.ItemDefinitionId));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Quantity_HasNoPublicSetter()
    {
        var property = typeof(CommunityInventoryEntry).GetProperty(
            nameof(CommunityInventoryEntry.Quantity));

        Assert.NotNull(property);
        Assert.Null(property!.GetSetMethod());
    }

    [Fact]
    public void Create_RejectsAnInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() =>
            CommunityInventoryEntry.Create(default, ItemDefinitionId.New()));
    }

    [Fact]
    public void Create_RejectsAnInvalidItemDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            CommunityInventoryEntry.Create(CommunityIdentityId.New(), default));
    }

    [Fact]
    public void Create_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(CommunityInventoryEntry).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public));
    }

    private static CommunityInventoryEntry CreateEntry() =>
        CommunityInventoryEntry.Create(
            CommunityIdentityId.New(),
            ItemDefinitionId.New());
}
