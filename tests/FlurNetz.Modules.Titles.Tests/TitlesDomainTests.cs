using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;
using System.Reflection;

namespace FlurNetz.Modules.Titles.Tests;

public sealed class TitleDefinitionIdTests
{
    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = TitleDefinitionId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd");

        var id = TitleDefinitionId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinitionId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd");

        Assert.Equal(TitleDefinitionId.Create(value), TitleDefinitionId.Create(value));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = TitleDefinitionId.Create(Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd"));
        var second = TitleDefinitionId.Create(Guid.Parse("ab0c7376-cf71-4d11-9794-f42832465170"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(TitleDefinitionId).GetProperty(nameof(TitleDefinitionId.Value));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }
}

public sealed class CommunityTitlesTests
{
    [Fact]
    public void Create_CarriesTheProvidedCommunityIdentityId()
    {
        var communityIdentityId = CommunityIdentityId.New();

        var titles = CommunityTitles.Create(communityIdentityId);

        Assert.Equal(communityIdentityId, titles.CommunityIdentityId);
    }

    [Fact]
    public void Create_StartsWithoutUnlockedOrCurrentTitle()
    {
        var titles = CreateTitles();

        Assert.Empty(titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void Create_RejectsInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityTitles.Create(default));
    }

    [Fact]
    public void Unlock_AddsTitleWithoutSelectingIt()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();

        var newlyUnlocked = titles.Unlock(titleDefinitionId);

        Assert.True(newlyUnlocked);
        Assert.Contains(titleDefinitionId, titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void Unlock_IsIdempotent()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();

        Assert.True(titles.Unlock(titleDefinitionId));
        Assert.False(titles.Unlock(titleDefinitionId));

        Assert.Single(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Unlock_AllowsDifferentTitles()
    {
        var titles = CreateTitles();
        var first = TitleDefinitionId.New();
        var second = TitleDefinitionId.New();

        titles.Unlock(first);
        titles.Unlock(second);

        Assert.Equal(2, titles.UnlockedTitleDefinitionIds.Count);
        Assert.Contains(first, titles.UnlockedTitleDefinitionIds);
        Assert.Contains(second, titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Unlock_RejectsInvalidTitleDefinitionIdWithoutChangingState()
    {
        var titles = CreateTitles();

        Assert.Throws<ArgumentException>(() => titles.Unlock(default));

        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void UnlockedTitles_AreExposedAsIndependentReadOnlySnapshots()
    {
        var titles = CreateTitles();
        titles.Unlock(TitleDefinitionId.New());

        var first = titles.UnlockedTitleDefinitionIds;
        var second = titles.UnlockedTitleDefinitionIds;

        Assert.NotSame(first, second);
        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public void SelectCurrentTitle_SelectsAnUnlockedTitle()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);

        titles.SelectCurrentTitle(titleDefinitionId);

        Assert.Equal(titleDefinitionId, titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SelectCurrentTitle_RejectsLockedTitleWithoutChangingCurrentSelection()
    {
        var titles = CreateTitles();
        var unlocked = TitleDefinitionId.New();
        var locked = TitleDefinitionId.New();
        titles.Unlock(unlocked);
        titles.SelectCurrentTitle(unlocked);

        Assert.Throws<TitleNotUnlockedException>(() => titles.SelectCurrentTitle(locked));

        Assert.Equal(unlocked, titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SelectCurrentTitle_RejectsInvalidTitleDefinitionId()
    {
        var titles = CreateTitles();

        Assert.Throws<ArgumentException>(() => titles.SelectCurrentTitle(default));

        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SelectCurrentTitle_CanSwitchBetweenUnlockedTitles()
    {
        var titles = CreateTitles();
        var first = TitleDefinitionId.New();
        var second = TitleDefinitionId.New();
        titles.Unlock(first);
        titles.Unlock(second);
        titles.SelectCurrentTitle(first);

        titles.SelectCurrentTitle(second);

        Assert.Equal(second, titles.CurrentTitleDefinitionId);
        Assert.Equal(2, titles.UnlockedTitleDefinitionIds.Count);
    }

    [Fact]
    public void SelectCurrentTitle_IsStableWhenSelectingTheCurrentTitleAgain()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);
        titles.SelectCurrentTitle(titleDefinitionId);

        titles.SelectCurrentTitle(titleDefinitionId);

        Assert.Equal(titleDefinitionId, titles.CurrentTitleDefinitionId);
        Assert.Single(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void UnlockingAnotherTitle_DoesNotReplaceCurrentSelection()
    {
        var titles = CreateTitles();
        var current = TitleDefinitionId.New();
        titles.Unlock(current);
        titles.SelectCurrentTitle(current);

        titles.Unlock(TitleDefinitionId.New());

        Assert.Equal(current, titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void ClearCurrentTitle_RemovesSelectionWithoutRemovingUnlocks()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);
        titles.SelectCurrentTitle(titleDefinitionId);

        titles.ClearCurrentTitle();

        Assert.Null(titles.CurrentTitleDefinitionId);
        Assert.Contains(titleDefinitionId, titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void ClearCurrentTitle_IsSafeWhenNoTitleIsSelected()
    {
        var titles = CreateTitles();

        titles.ClearCurrentTitle();

        Assert.Null(titles.CurrentTitleDefinitionId);
        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void CommunityIdentityId_IsImmutable()
    {
        var property = typeof(CommunityTitles).GetProperty(nameof(CommunityTitles.CommunityIdentityId));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void CurrentTitleDefinitionId_HasNoPublicSetter()
    {
        var property = typeof(CommunityTitles).GetProperty(nameof(CommunityTitles.CurrentTitleDefinitionId));

        Assert.NotNull(property);
        Assert.Null(property!.GetSetMethod());
    }

    [Fact]
    public void UnlockedTitleDefinitionIds_HasNoSetter()
    {
        var property = typeof(CommunityTitles).GetProperty(nameof(CommunityTitles.UnlockedTitleDefinitionIds));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void CommunityTitles_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(CommunityTitles).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void Foundation_HasNoPublicRehydratePath()
    {
        var rehydrateMethods = typeof(CommunityTitles)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Rehydrate")
            .ToArray();

        Assert.Empty(rehydrateMethods);
    }

    private static CommunityTitles CreateTitles() =>
        CommunityTitles.Create(CommunityIdentityId.New());
}
