using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Domain;

namespace FlurNetz.Modules.Progression.Tests;

public sealed class ExperiencePointsTests
{
    [Fact]
    public void Zero_IsValid()
    {
        Assert.Equal(0, ExperiencePoints.Zero.Value);
        Assert.Equal(ExperiencePoints.Zero, ExperiencePoints.Create(0));
    }

    [Fact]
    public void Create_AcceptsPositiveValues()
    {
        var points = ExperiencePoints.Create(42);

        Assert.Equal(42, points.Value);
    }

    [Fact]
    public void Create_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperiencePoints.Create(-1));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        Assert.Equal(ExperiencePoints.Create(42), ExperiencePoints.Create(42));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(ExperiencePoints.Create(41), ExperiencePoints.Create(42));
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(ExperiencePoints).GetProperty(nameof(ExperiencePoints.Value));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Add_AccumulatesPositiveValuesWithoutMutatingTheOriginal()
    {
        var original = ExperiencePoints.Create(10);

        var result = original.Add(5);

        Assert.Equal(15, result.Value);
        Assert.Equal(10, original.Value);
    }

    [Fact]
    public void AddZero_LeavesTheValueUnchanged()
    {
        var points = ExperiencePoints.Create(42);

        Assert.Equal(points, points.Add(0));
    }

    [Fact]
    public void Add_RejectsNegativeAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperiencePoints.Zero.Add(-1));
    }

    [Fact]
    public void Add_RejectsOverflow()
    {
        var points = ExperiencePoints.Create(long.MaxValue);

        Assert.Throws<OverflowException>(() => points.Add(1));
    }
}

public sealed class CommunityProgressionTests
{
    [Fact]
    public void Create_CarriesTheProvidedCommunityIdentityId()
    {
        var communityIdentityId = CommunityIdentityId.New();

        var progression = CommunityProgression.Create(communityIdentityId);

        Assert.Equal(communityIdentityId, progression.CommunityIdentityId);
    }

    [Fact]
    public void Create_StartsWithZeroExperiencePoints()
    {
        var progression = CommunityProgression.Create(CommunityIdentityId.New());

        Assert.Equal(ExperiencePoints.Zero, progression.ExperiencePoints);
    }

    [Fact]
    public void GrantExperience_AddsPositiveExperiencePoints()
    {
        var progression = CommunityProgression.Create(CommunityIdentityId.New());

        progression.GrantExperience(25);

        Assert.Equal(25, progression.ExperiencePoints.Value);
    }

    [Fact]
    public void GrantExperience_AccumulatesMultipleGrants()
    {
        var progression = CommunityProgression.Create(CommunityIdentityId.New());

        progression.GrantExperience(10);
        progression.GrantExperience(7);

        Assert.Equal(17, progression.ExperiencePoints.Value);
    }

    [Fact]
    public void GrantExperience_RejectsZeroAndNegativeAmounts()
    {
        var progression = CommunityProgression.Create(CommunityIdentityId.New());

        Assert.Throws<ArgumentOutOfRangeException>(() => progression.GrantExperience(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => progression.GrantExperience(-1));
    }

    [Fact]
    public void GrantExperience_RejectsOverflow()
    {
        var progression = CommunityProgression.Create(CommunityIdentityId.New());
        progression.GrantExperience(long.MaxValue);

        Assert.Throws<OverflowException>(() => progression.GrantExperience(1));
    }

    [Fact]
    public void CommunityIdentityId_IsImmutable()
    {
        var property = typeof(CommunityProgression).GetProperty(nameof(CommunityProgression.CommunityIdentityId));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Create_RejectsAnInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityProgression.Create(default));
    }
}
