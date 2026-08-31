using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Tests;

public sealed class AchievementDefinitionIdTests
{
    [Fact]
    public void Create_AcceptsNonEmptyGuidAndPreservesValue()
    {
        var value = Guid.Parse("b7f954f9-b824-49ea-b47d-2ffbf21817fd");

        var id = AchievementDefinitionId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => AchievementDefinitionId.Create(Guid.Empty));
    }

    [Fact]
    public void New_CreatesAValidId()
    {
        Assert.NotEqual(Guid.Empty, AchievementDefinitionId.New().Value);
    }
}

public sealed class AchievementDefinitionTests
{
    [Fact]
    public void Create_NormalizesNameAndDescriptionAndPreservesId()
    {
        var id = AchievementDefinitionId.New();

        var definition = AchievementDefinition.Create(id, "  Champion  ", "  Beschreibung  ");

        Assert.Equal(id, definition.Id);
        Assert.Equal("Champion", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public void Create_NormalizesUnicodeWhitespace()
    {
        var definition = AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            "\u2003Name\u00a0",
            "\u202fText\u3000");

        Assert.Equal("Name", definition.DisplayName);
        Assert.Equal("Text", definition.Description);
    }

    [Fact]
    public void Create_MapsBlankDescriptionToNull()
    {
        var definition = AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            "Name",
            " \u2003\u00a0 ");

        Assert.Null(definition.Description);
    }

    [Fact]
    public void Create_RejectsBlankDisplayName()
    {
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            " \u2003\u00a0 ",
            null));
    }

    [Fact]
    public void Create_AcceptsMaximumLengths()
    {
        var definition = AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            new string('a', AchievementDefinition.MaxDisplayNameLength),
            new string('b', AchievementDefinition.MaxDescriptionLength));

        Assert.Equal(AchievementDefinition.MaxDisplayNameLength, definition.DisplayName.Length);
        Assert.Equal(AchievementDefinition.MaxDescriptionLength, definition.Description!.Length);
    }

    [Fact]
    public void Create_RejectsValuesLongerThanMaximums()
    {
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            new string('a', AchievementDefinition.MaxDisplayNameLength + 1),
            null));
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Create(
            AchievementDefinitionId.New(),
            "Name",
            new string('b', AchievementDefinition.MaxDescriptionLength + 1)));
    }

    [Fact]
    public void Rehydrate_RejectsNonCanonicalPersistedTextInsteadOfRepairingIt()
    {
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            AchievementDefinitionId.New(),
            " Name ",
            null));
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            AchievementDefinitionId.New(),
            "Name",
            "  Beschreibung"));
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            AchievementDefinitionId.New(),
            "Name",
            " \u2003"));
    }

    [Fact]
    public void Rehydrate_EnforcesTheSameLengthAndIdRules()
    {
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            default,
            "Name",
            null));
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            AchievementDefinitionId.New(),
            new string('a', AchievementDefinition.MaxDisplayNameLength + 1),
            null));
        Assert.Throws<ArgumentException>(() => AchievementDefinition.Rehydrate(
            AchievementDefinitionId.New(),
            "Name",
            new string('b', AchievementDefinition.MaxDescriptionLength + 1)));
    }

    [Fact]
    public void Rename_ReturnsTrueOnlyForAnActualCanonicalChange()
    {
        var definition = AchievementDefinition.Create(AchievementDefinitionId.New(), "Name", null);

        Assert.False(definition.Rename(" Name "));
        Assert.True(definition.Rename("Neu"));
        Assert.Equal("Neu", definition.DisplayName);
    }

    [Fact]
    public void ChangeDescription_ReturnsTrueOnlyForAnActualCanonicalChange()
    {
        var definition = AchievementDefinition.Create(AchievementDefinitionId.New(), "Name", null);

        Assert.False(definition.ChangeDescription(" \u2003"));
        Assert.True(definition.ChangeDescription("  Text  "));
        Assert.Equal("Text", definition.Description);
        Assert.False(definition.ChangeDescription("Text"));
    }
}

public sealed class CommunityAchievementTests
{
    [Fact]
    public void CreateAndRehydratePreserveTheImmutableValues()
    {
        var communityIdentityId = CommunityIdentityId.New();
        var definitionId = AchievementDefinitionId.New();
        var unlockedAtUtc = new DateTimeOffset(2026, 8, 31, 12, 30, 0, TimeSpan.Zero);

        var created = CommunityAchievement.Create(communityIdentityId, definitionId, unlockedAtUtc);
        var rehydrated = CommunityAchievement.Rehydrate(communityIdentityId, definitionId, unlockedAtUtc);

        Assert.Equal(communityIdentityId, created.CommunityIdentityId);
        Assert.Equal(definitionId, created.AchievementDefinitionId);
        Assert.Equal(unlockedAtUtc, created.UnlockedAtUtc);
        Assert.Equal(created.CommunityIdentityId, rehydrated.CommunityIdentityId);
        Assert.Equal(created.AchievementDefinitionId, rehydrated.AchievementDefinitionId);
        Assert.Equal(created.UnlockedAtUtc, rehydrated.UnlockedAtUtc);
        Assert.All(typeof(CommunityAchievement).GetProperties(), property =>
            Assert.Null(property.SetMethod));
    }

    [Fact]
    public void CreateAndRehydrateRejectEmptyIds()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => CommunityAchievement.Create(
            default,
            AchievementDefinitionId.New(),
            timestamp));
        Assert.Throws<ArgumentException>(() => CommunityAchievement.Create(
            CommunityIdentityId.New(),
            default,
            timestamp));
        Assert.Throws<ArgumentException>(() => CommunityAchievement.Rehydrate(
            default,
            AchievementDefinitionId.New(),
            timestamp));
    }

    [Fact]
    public void CreateAndRehydrateRejectNonUtcValues()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 31, 14, 30, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => CommunityAchievement.Create(
            CommunityIdentityId.New(),
            AchievementDefinitionId.New(),
            nonUtc));
        Assert.Throws<ArgumentException>(() => CommunityAchievement.Rehydrate(
            CommunityIdentityId.New(),
            AchievementDefinitionId.New(),
            nonUtc));
    }
}
