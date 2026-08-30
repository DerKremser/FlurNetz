using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Application;
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

    [Fact]
    public void Rehydrate_ReconstructsZeroAndPositiveExperiencePoints()
    {
        var communityIdentityId = CommunityIdentityId.New();

        var zero = CommunityProgression.Rehydrate(communityIdentityId, ExperiencePoints.Zero);
        var positive = CommunityProgression.Rehydrate(communityIdentityId, ExperiencePoints.Create(42));

        Assert.Equal(communityIdentityId, zero.CommunityIdentityId);
        Assert.Equal(ExperiencePoints.Zero, zero.ExperiencePoints);
        Assert.Equal(communityIdentityId, positive.CommunityIdentityId);
        Assert.Equal(42, positive.ExperiencePoints.Value);
    }

    [Fact]
    public void Rehydrate_RejectsAnInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() =>
            CommunityProgression.Rehydrate(default, ExperiencePoints.Zero));
    }
}

public sealed class GrantExperienceTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesArgumentsAndReturnsTheNewTotal()
    {
        var store = new RecordingProgressionStore(ExperiencePoints.Create(25));
        var useCase = new GrantExperience(store);
        var communityIdentityId = CommunityIdentityId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(
            communityIdentityId,
            7,
            cancellationSource.Token);

        Assert.Equal(ExperiencePoints.Create(25), result);
        Assert.Equal(communityIdentityId, store.CommunityIdentityId);
        Assert.Equal(7, store.Amount);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
    }

    private sealed class RecordingProgressionStore(ExperiencePoints result) : ICommunityProgressionStore
    {
        public CommunityIdentityId CommunityIdentityId { get; private set; }

        public long Amount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<ExperiencePoints> GrantExperienceAsync(
            CommunityIdentityId communityIdentityId,
            long amount,
            CancellationToken cancellationToken = default)
        {
            CommunityIdentityId = communityIdentityId;
            Amount = amount;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }

        public Task<CommunityProgression?> GetByCommunityIdentityIdAsync(
            CommunityIdentityId communityIdentityId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CommunityProgression?>(null);
        }
    }
}
