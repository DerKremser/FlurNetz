using System.Reflection;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;

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
    public void Create_AcceptsNonEmptyGuidAndPreservesValue()
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
    public void EqualValues_HaveValueSemantics()
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
    public void Create_RejectsInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityTitles.Create(default));
    }

    [Fact]
    public void Create_StartsWithoutUnlockedOrCurrentTitle()
    {
        var titles = CreateTitles();

        Assert.Empty(titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void Unlock_AddsTitleAndReturnsTrueWithoutSelectingIt()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();

        var changed = titles.Unlock(titleDefinitionId);

        Assert.True(changed);
        Assert.True(titles.IsUnlocked(titleDefinitionId));
        Assert.Contains(titleDefinitionId, titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void Unlock_IsIdempotentAndDoesNotCreateDuplicates()
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
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void IsUnlocked_RejectsInvalidTitleDefinitionId()
    {
        var titles = CreateTitles();

        Assert.Throws<ArgumentException>(() => titles.IsUnlocked(default));
    }

    [Fact]
    public void UnlockedTitles_AreExposedAsIndependentReadOnlySnapshots()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);

        var first = titles.UnlockedTitleDefinitionIds;
        var second = titles.UnlockedTitleDefinitionIds;

        Assert.NotSame(first, second);
        Assert.Single(first);
        Assert.Single(second);

        titles.Lock(titleDefinitionId);

        Assert.Contains(titleDefinitionId, first);
        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Lock_RemovesExistingTitleAndReturnsTrue()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);

        var changed = titles.Lock(titleDefinitionId);

        Assert.True(changed);
        Assert.False(titles.IsUnlocked(titleDefinitionId));
        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Lock_IsIdempotentForMissingTitle()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();

        Assert.False(titles.Lock(titleDefinitionId));
        Assert.False(titles.Lock(titleDefinitionId));

        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Lock_RejectsInvalidTitleDefinitionIdWithoutChangingState()
    {
        var titles = CreateTitles();

        Assert.Throws<ArgumentException>(() => titles.Lock(default));

        Assert.Empty(titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SetCurrent_SelectsAnUnlockedTitleAndReturnsTrue()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);

        var changed = titles.SetCurrent(titleDefinitionId);

        Assert.True(changed);
        Assert.Equal(titleDefinitionId, titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SetCurrent_IsIdempotentForTheCurrentTitle()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);
        titles.SetCurrent(titleDefinitionId);

        var changed = titles.SetCurrent(titleDefinitionId);

        Assert.False(changed);
        Assert.Equal(titleDefinitionId, titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void SetCurrent_ReplacesAnotherUnlockedCurrentTitle()
    {
        var titles = CreateTitles();
        var first = TitleDefinitionId.New();
        var second = TitleDefinitionId.New();
        titles.Unlock(first);
        titles.Unlock(second);
        titles.SetCurrent(first);

        var changed = titles.SetCurrent(second);

        Assert.True(changed);
        Assert.Equal(second, titles.CurrentTitleDefinitionId);
        Assert.Equal(2, titles.UnlockedTitleDefinitionIds.Count);
    }

    [Fact]
    public void SetCurrent_RejectsLockedTitleWithoutChangingExistingState()
    {
        var titles = CreateTitles();
        var current = TitleDefinitionId.New();
        var locked = TitleDefinitionId.New();
        titles.Unlock(current);
        titles.SetCurrent(current);

        Assert.Throws<TitleNotUnlockedException>(() => titles.SetCurrent(locked));

        Assert.Equal(current, titles.CurrentTitleDefinitionId);
        Assert.Single(titles.UnlockedTitleDefinitionIds);
        Assert.True(titles.IsUnlocked(current));
        Assert.False(titles.IsUnlocked(locked));
    }

    [Fact]
    public void SetCurrent_RejectsInvalidTitleDefinitionIdWithoutChangingState()
    {
        var titles = CreateTitles();

        Assert.Throws<ArgumentException>(() => titles.SetCurrent(default));

        Assert.Null(titles.CurrentTitleDefinitionId);
        Assert.Empty(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void ClearCurrent_RemovesSelectionAndKeepsUnlocks()
    {
        var titles = CreateTitles();
        var titleDefinitionId = TitleDefinitionId.New();
        titles.Unlock(titleDefinitionId);
        titles.SetCurrent(titleDefinitionId);

        var changed = titles.ClearCurrent();

        Assert.True(changed);
        Assert.Null(titles.CurrentTitleDefinitionId);
        Assert.True(titles.IsUnlocked(titleDefinitionId));
        Assert.Single(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void ClearCurrent_IsIdempotentWithoutSelection()
    {
        var titles = CreateTitles();

        Assert.False(titles.ClearCurrent());
        Assert.False(titles.ClearCurrent());

        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void LockingCurrentTitle_RemovesSelectionAtTheSameTime()
    {
        var titles = CreateTitles();
        var current = TitleDefinitionId.New();
        var other = TitleDefinitionId.New();
        titles.Unlock(current);
        titles.Unlock(other);
        titles.SetCurrent(current);

        var changed = titles.Lock(current);

        Assert.True(changed);
        Assert.Null(titles.CurrentTitleDefinitionId);
        Assert.False(titles.IsUnlocked(current));
        Assert.True(titles.IsUnlocked(other));
    }

    [Fact]
    public void LockingOtherTitle_LeavesCurrentTitleUnchanged()
    {
        var titles = CreateTitles();
        var current = TitleDefinitionId.New();
        var other = TitleDefinitionId.New();
        titles.Unlock(current);
        titles.Unlock(other);
        titles.SetCurrent(current);

        titles.Lock(other);

        Assert.Equal(current, titles.CurrentTitleDefinitionId);
        Assert.True(titles.IsUnlocked(current));
        Assert.False(titles.IsUnlocked(other));
    }

    [Fact]
    public void PublicStatePropertiesCannotBeSetExternally()
    {
        var communityIdentityId = typeof(CommunityTitles)
            .GetProperty(nameof(CommunityTitles.CommunityIdentityId));
        var currentTitleDefinitionId = typeof(CommunityTitles)
            .GetProperty(nameof(CommunityTitles.CurrentTitleDefinitionId));
        var unlockedTitleDefinitionIds = typeof(CommunityTitles)
            .GetProperty(nameof(CommunityTitles.UnlockedTitleDefinitionIds));

        Assert.NotNull(communityIdentityId);
        Assert.Null(communityIdentityId!.SetMethod);
        Assert.NotNull(currentTitleDefinitionId);
        Assert.Null(currentTitleDefinitionId!.GetSetMethod());
        Assert.NotNull(unlockedTitleDefinitionIds);
        Assert.Null(unlockedTitleDefinitionIds!.SetMethod);
    }

    [Fact]
    public void CommunityTitles_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(CommunityTitles).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void Rehydrate_StartsWithoutUnlocksOrCurrent()
    {
        var communityIdentityId = CommunityIdentityId.New();

        var titles = CommunityTitles.Rehydrate(
            communityIdentityId,
            [],
            null);

        Assert.Equal(communityIdentityId, titles.CommunityIdentityId);
        Assert.Empty(titles.UnlockedTitleDefinitionIds);
        Assert.Null(titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public void Rehydrate_RestoresMultipleUnlocks()
    {
        var first = TitleDefinitionId.New();
        var second = TitleDefinitionId.New();

        var titles = CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [first, second],
            null);

        Assert.Equal(2, titles.UnlockedTitleDefinitionIds.Count);
        Assert.Contains(first, titles.UnlockedTitleDefinitionIds);
        Assert.Contains(second, titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Rehydrate_RestoresCurrentWhenItIsUnlocked()
    {
        var current = TitleDefinitionId.New();

        var titles = CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [current],
            current);

        Assert.Equal(current, titles.CurrentTitleDefinitionId);
        Assert.True(titles.IsUnlocked(current));
    }

    [Fact]
    public void Rehydrate_RequiresCurrentToBeUnlocked()
    {
        var current = TitleDefinitionId.New();

        Assert.Throws<TitleNotUnlockedException>(() => CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [],
            current));
    }

    [Fact]
    public void Rehydrate_RejectsInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityTitles.Rehydrate(
            default,
            [],
            null));
    }

    [Fact]
    public void Rehydrate_RejectsNullUnlockCollection()
    {
        Assert.Throws<ArgumentNullException>(() => CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            null!,
            null));
    }

    [Fact]
    public void Rehydrate_RejectsInvalidUnlock()
    {
        Assert.Throws<ArgumentException>(() => CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [default(TitleDefinitionId)],
            null));
    }

    [Fact]
    public void Rehydrate_RejectsInvalidCurrent()
    {
        TitleDefinitionId? invalidCurrent = default(TitleDefinitionId);

        Assert.Throws<ArgumentException>(() => CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [],
            invalidCurrent));
    }

    [Fact]
    public void Rehydrate_UnifiesDuplicateUnlockIds()
    {
        var titleDefinitionId = TitleDefinitionId.New();

        var titles = CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [titleDefinitionId, titleDefinitionId],
            null);

        Assert.Single(titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Rehydrate_CopiesTheInputCollection()
    {
        var titleDefinitionId = TitleDefinitionId.New();
        var input = new List<TitleDefinitionId> { titleDefinitionId };

        var titles = CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            input,
            null);
        input.Clear();

        Assert.Single(titles.UnlockedTitleDefinitionIds);
        Assert.Contains(titleDefinitionId, titles.UnlockedTitleDefinitionIds);
    }

    [Fact]
    public void Rehydrate_KeepsCurrentInsideTheUnlockSet()
    {
        var first = TitleDefinitionId.New();
        var current = TitleDefinitionId.New();

        var titles = CommunityTitles.Rehydrate(
            CommunityIdentityId.New(),
            [first, current],
            current);

        Assert.Contains(titles.CurrentTitleDefinitionId!.Value, titles.UnlockedTitleDefinitionIds);
    }

    private static CommunityTitles CreateTitles() =>
        CommunityTitles.Create(CommunityIdentityId.New());
}

public sealed class TitleDefinitionTests
{
    [Fact]
    public void Create_PreservesIdAndCanonicalizesValues()
    {
        var id = TitleDefinitionId.New();

        var definition = TitleDefinition.Create(
            id,
            "  Veteran  ",
            "  Eine Beschreibung  ");

        Assert.Equal(id, definition.Id);
        Assert.Equal("Veteran", definition.DisplayName);
        Assert.Equal("Eine Beschreibung", definition.Description);
    }

    [Fact]
    public void Create_CanonicalizesTabAndUnicodeWhitespaceAroundValues()
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "\t\u00A0Veteran\u00A0\t",
            "\t\u2003Beschreibung\u2003\t");

        Assert.Equal("Veteran", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public void Create_RejectsUnicodeWhitespaceOnlyDisplayNameAndCanonicalizesDescriptionToNull()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            "\t\u00A0\u2003",
            null));

        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            "\t\u00A0\u2003");

        Assert.Null(definition.Description);
    }

    [Fact]
    public void Create_RejectsNullDisplayName()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            null!,
            null));
    }

    [Fact]
    public void Create_RejectsEmptyDisplayName()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            string.Empty,
            null));
    }

    [Fact]
    public void Create_RejectsWhitespaceDisplayName()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            "   ",
            null));
    }

    [Fact]
    public void Create_AllowsDisplayNameAtMaximumLength()
    {
        var displayName = new string('x', TitleDefinition.MaxDisplayNameLength);

        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            displayName,
            null);

        Assert.Equal(displayName, definition.DisplayName);
    }

    [Fact]
    public void Create_RejectsDisplayNameAboveMaximumLength()
    {
        var displayName = new string('x', TitleDefinition.MaxDisplayNameLength + 1);

        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            displayName,
            null));
    }

    [Fact]
    public void Create_AllowsNullDescription()
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            null);

        Assert.Null(definition.Description);
    }

    [Fact]
    public void Create_CanonicalizesEmptyDescriptionToNull()
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            string.Empty);

        Assert.Null(definition.Description);
    }

    [Fact]
    public void Create_CanonicalizesWhitespaceDescriptionToNull()
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            "   ");

        Assert.Null(definition.Description);
    }

    [Fact]
    public void Create_AllowsDescriptionAtMaximumLength()
    {
        var description = new string('x', TitleDefinition.MaxDescriptionLength);

        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            description);

        Assert.Equal(description, definition.Description);
    }

    [Fact]
    public void Create_RejectsDescriptionAboveMaximumLength()
    {
        var description = new string('x', TitleDefinition.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            description));
    }

    [Fact]
    public void Create_RejectsDefaultId()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Create(
            default,
            "Veteran",
            null));
    }

    [Fact]
    public void Rehydrate_RestoresValidCanonicalState()
    {
        var id = TitleDefinitionId.New();

        var definition = TitleDefinition.Rehydrate(
            id,
            "  Veteran  ",
            "  Beschreibung  ");

        Assert.Equal(id, definition.Id);
        Assert.Equal("Veteran", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public void Rehydrate_UsesTheSameValidationRulesAsCreate()
    {
        Assert.Throws<ArgumentException>(() => TitleDefinition.Rehydrate(
            TitleDefinitionId.New(),
            "   ",
            null));
        Assert.Throws<ArgumentException>(() => TitleDefinition.Rehydrate(
            TitleDefinitionId.New(),
            "Veteran",
            new string('x', TitleDefinition.MaxDescriptionLength + 1)));
    }

    [Fact]
    public void Rename_ChangesNameAndPreservesOtherState()
    {
        var definition = CreateDefinition();

        var changed = definition.Rename("  Champion  ");

        Assert.True(changed);
        Assert.Equal("Champion", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
        Assert.Equal(CreateDefinitionId, definition.Id);
    }

    [Fact]
    public void Rename_ReturnsFalseForTheSameCanonicalName()
    {
        var definition = CreateDefinition();

        var changed = definition.Rename("  Veteran  ");

        Assert.False(changed);
        Assert.Equal("Veteran", definition.DisplayName);
    }

    [Fact]
    public void Rename_TrimsTheNewName()
    {
        var definition = CreateDefinition();

        definition.Rename("  Champion  ");

        Assert.Equal("Champion", definition.DisplayName);
    }

    [Fact]
    public void Rename_RejectsInvalidNameWithoutChangingState()
    {
        var definition = CreateDefinition();

        Assert.Throws<ArgumentException>(() => definition.Rename("   "));

        Assert.Equal("Veteran", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public void Rename_RejectsNameAboveMaximumLengthWithoutChangingState()
    {
        var definition = CreateDefinition();

        Assert.Throws<ArgumentException>(() => definition.Rename(
            new string('x', TitleDefinition.MaxDisplayNameLength + 1)));

        Assert.Equal("Veteran", definition.DisplayName);
    }

    [Fact]
    public void ChangeDescription_SetsAndChangesDescription()
    {
        var definition = CreateDefinition();

        Assert.True(definition.ChangeDescription("  Neu  "));
        Assert.Equal("Neu", definition.Description);
        Assert.True(definition.ChangeDescription("Anders"));
        Assert.Equal("Anders", definition.Description);
    }

    [Fact]
    public void ChangeDescription_RemovesDescriptionWithNull()
    {
        var definition = CreateDefinition();

        var changed = definition.ChangeDescription(null);

        Assert.True(changed);
        Assert.Null(definition.Description);
    }

    [Fact]
    public void ChangeDescription_RemovesDescriptionWithWhitespace()
    {
        var definition = CreateDefinition();

        var changed = definition.ChangeDescription("   ");

        Assert.True(changed);
        Assert.Null(definition.Description);
    }

    [Fact]
    public void ChangeDescription_ReturnsFalseForTheSameCanonicalValue()
    {
        var definition = CreateDefinition();

        var changed = definition.ChangeDescription("  Beschreibung  ");

        Assert.False(changed);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public void ChangeDescription_RejectsTooLongValueWithoutChangingState()
    {
        var definition = CreateDefinition();

        Assert.Throws<ArgumentException>(() => definition.ChangeDescription(
            new string('x', TitleDefinition.MaxDescriptionLength + 1)));

        Assert.Equal("Beschreibung", definition.Description);
        Assert.Equal("Veteran", definition.DisplayName);
    }

    private static TitleDefinitionId CreateDefinitionId { get; } = TitleDefinitionId.New();

    private static TitleDefinition CreateDefinition() =>
        TitleDefinition.Create(CreateDefinitionId, "Veteran", "Beschreibung");
}
