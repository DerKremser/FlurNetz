using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Tests;

public sealed class AchievementsApplicationTests
{
    [Fact]
    public async Task CreateGeneratesAnIdAndPersistsExactlyOnce()
    {
        var store = new FakeDefinitionStore();
        var useCase = new CreateAchievementDefinition(store);

        var definition = await useCase.ExecuteAsync(
            "  Champion  ",
            "  Beschreibung  ",
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, definition.Id.Value);
        Assert.Same(definition, store.AddedDefinition);
        Assert.Equal(1, store.AddCallCount);
        Assert.Equal("Champion", definition.DisplayName);
        Assert.Equal("Beschreibung", definition.Description);
    }

    [Fact]
    public async Task CatalogReadsAndMutationsDelegateCorrectly()
    {
        var id = AchievementDefinitionId.New();
        var definition = AchievementDefinition.Create(id, "Alt", "Beschreibung");
        var second = AchievementDefinition.Create(AchievementDefinitionId.New(), "Zweit", null);
        var store = new FakeDefinitionStore
        {
            Definition = definition,
            ListResult = Array.AsReadOnly<AchievementDefinition>([definition, second])
        };

        Assert.Same(definition, await new GetAchievementDefinition(store).ExecuteAsync(
            id,
            TestContext.Current.CancellationToken));
        Assert.Same(store.ListResult, await new ListAchievementDefinitions(store).ExecuteAsync(
            TestContext.Current.CancellationToken));
        Assert.True(await new RenameAchievementDefinition(store).ExecuteAsync(
            id,
            "Neu",
            TestContext.Current.CancellationToken));
        Assert.True(await new ChangeAchievementDescription(store).ExecuteAsync(
            id,
            "Neu",
            TestContext.Current.CancellationToken));
        Assert.Equal(id, store.LastExecutedId);
        Assert.Equal("Neu", definition.DisplayName);
        Assert.Equal("Neu", definition.Description);
    }

    [Fact]
    public async Task UnknownCatalogMutationPropagatesNotFound()
    {
        var id = AchievementDefinitionId.New();
        var store = new FakeDefinitionStore();

        await Assert.ThrowsAsync<AchievementDefinitionNotFoundException>(() =>
            new RenameAchievementDefinition(store).ExecuteAsync(
                id,
                "Name",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnlockUsesTheExactClockValueAndPropagatesTrue()
    {
        var communityIdentityId = CommunityIdentityId.New();
        var definitionId = AchievementDefinitionId.New();
        var timestamp = new DateTimeOffset(2026, 8, 31, 10, 15, 0, TimeSpan.Zero);
        var definitionStore = new FakeDefinitionStore
        {
            Definition = AchievementDefinition.Create(definitionId, "Name", null)
        };
        var achievementStore = new FakeCommunityAchievementStore { Result = true };
        var useCase = new UnlockCommunityAchievement(
            definitionStore,
            achievementStore,
            new FixedClock(timestamp));

        var result = await useCase.ExecuteAsync(
            communityIdentityId,
            definitionId,
            TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.NotNull(achievementStore.UnlockedAchievement);
        Assert.Equal(communityIdentityId, achievementStore.UnlockedAchievement!.CommunityIdentityId);
        Assert.Equal(definitionId, achievementStore.UnlockedAchievement.AchievementDefinitionId);
        Assert.Equal(timestamp, achievementStore.UnlockedAchievement.UnlockedAtUtc);
        Assert.Equal(1, achievementStore.CallCount);
    }

    [Fact]
    public async Task UnlockPropagatesFalseForAnIdempotentStoreNoOp()
    {
        var definitionId = AchievementDefinitionId.New();
        var definitionStore = new FakeDefinitionStore
        {
            Definition = AchievementDefinition.Create(definitionId, "Name", null)
        };
        var achievementStore = new FakeCommunityAchievementStore { Result = false };
        var useCase = new UnlockCommunityAchievement(
            definitionStore,
            achievementStore,
            new FixedClock(DateTimeOffset.UtcNow));

        var result = await useCase.ExecuteAsync(
            CommunityIdentityId.New(),
            definitionId,
            TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task UnknownDefinitionStopsBeforeCommunityStore()
    {
        var achievementStore = new FakeCommunityAchievementStore();
        var useCase = new UnlockCommunityAchievement(
            new FakeDefinitionStore(),
            achievementStore,
            new FixedClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<AchievementDefinitionNotFoundException>(() => useCase.ExecuteAsync(
            CommunityIdentityId.New(),
            AchievementDefinitionId.New(),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, achievementStore.CallCount);
    }

    [Fact]
    public async Task CommunityReadsDelegateAndListIsReturnedAsProvided()
    {
        var communityIdentityId = CommunityIdentityId.New();
        var definitionId = AchievementDefinitionId.New();
        var achievement = CommunityAchievement.Create(
            communityIdentityId,
            definitionId,
            DateTimeOffset.UtcNow);
        var list = Array.AsReadOnly<CommunityAchievement>([achievement]);
        var store = new FakeCommunityAchievementStore
        {
            GetResult = achievement,
            ListResult = list
        };

        Assert.Same(achievement, await new GetCommunityAchievement(store).ExecuteAsync(
            communityIdentityId,
            definitionId,
            TestContext.Current.CancellationToken));
        Assert.Same(list, await new ListCommunityAchievements(store).ExecuteAsync(
            communityIdentityId,
            TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeDefinitionStore : IAchievementDefinitionStore
    {
        public AchievementDefinition? Definition { get; init; }

        public IReadOnlyList<AchievementDefinition> ListResult { get; init; } =
            Array.AsReadOnly(Array.Empty<AchievementDefinition>());

        public AchievementDefinition? AddedDefinition { get; private set; }

        public int AddCallCount { get; private set; }

        public AchievementDefinitionId? LastExecutedId { get; private set; }

        public Task AddAsync(AchievementDefinition definition, CancellationToken cancellationToken = default)
        {
            AddedDefinition = definition;
            AddCallCount++;
            return Task.CompletedTask;
        }

        public Task<AchievementDefinition?> GetAsync(
            AchievementDefinitionId achievementDefinitionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Definition);
        }

        public Task<IReadOnlyList<AchievementDefinition>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ListResult);
        }

        public Task<TResult> ExecuteAsync<TResult>(
            AchievementDefinitionId achievementDefinitionId,
            Func<AchievementDefinition, TResult> operation,
            CancellationToken cancellationToken = default)
        {
            LastExecutedId = achievementDefinitionId;
            if (Definition is null)
            {
                throw new AchievementDefinitionNotFoundException(achievementDefinitionId);
            }

            return Task.FromResult(operation(Definition));
        }
    }

    private sealed class FakeCommunityAchievementStore : ICommunityAchievementStore
    {
        public bool Result { get; init; }

        public CommunityAchievement? UnlockedAchievement { get; private set; }

        public CommunityAchievement? GetResult { get; init; }

        public IReadOnlyList<CommunityAchievement> ListResult { get; init; } =
            Array.AsReadOnly(Array.Empty<CommunityAchievement>());

        public int CallCount { get; private set; }

        public Task<bool> UnlockAsync(
            CommunityAchievement achievement,
            CancellationToken cancellationToken = default)
        {
            UnlockedAchievement = achievement;
            CallCount++;
            return Task.FromResult(Result);
        }

        public Task<CommunityAchievement?> GetAsync(
            CommunityIdentityId communityIdentityId,
            AchievementDefinitionId achievementDefinitionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetResult);
        }

        public Task<IReadOnlyList<CommunityAchievement>> ListAsync(
            CommunityIdentityId communityIdentityId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ListResult);
        }
    }
}
