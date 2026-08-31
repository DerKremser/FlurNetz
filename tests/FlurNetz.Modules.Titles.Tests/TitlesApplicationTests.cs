using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Tests;

public sealed class TitlesApplicationTests
{
    [Fact]
    public async Task UnlockCommunityTitleExecutesDomainUnlockThroughTheStore()
    {
        var store = new InMemoryTitlesStore();
        var useCase = new UnlockCommunityTitle(store);
        var communityIdentityId = store.Titles.CommunityIdentityId;
        var titleDefinitionId = TitleDefinitionId.New();

        var changed = await useCase.ExecuteAsync(
            communityIdentityId,
            titleDefinitionId,
            TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(communityIdentityId, store.Titles.CommunityIdentityId);
        Assert.True(store.Titles.IsUnlocked(titleDefinitionId));
    }

    [Fact]
    public async Task LockCommunityTitleExecutesDomainLockThroughTheStore()
    {
        var store = new InMemoryTitlesStore();
        var titleDefinitionId = TitleDefinitionId.New();
        store.Titles.Unlock(titleDefinitionId);
        var useCase = new LockCommunityTitle(store);

        var changed = await useCase.ExecuteAsync(
            store.Titles.CommunityIdentityId,
            titleDefinitionId,
            TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.False(store.Titles.IsUnlocked(titleDefinitionId));
    }

    [Fact]
    public async Task SetCurrentCommunityTitleExecutesDomainSelectionThroughTheStore()
    {
        var store = new InMemoryTitlesStore();
        var titleDefinitionId = TitleDefinitionId.New();
        store.Titles.Unlock(titleDefinitionId);
        var useCase = new SetCurrentCommunityTitle(store);

        var changed = await useCase.ExecuteAsync(
            store.Titles.CommunityIdentityId,
            titleDefinitionId,
            TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(titleDefinitionId, store.Titles.CurrentTitleDefinitionId);
    }

    [Fact]
    public async Task ClearCurrentCommunityTitleExecutesDomainClearThroughTheStore()
    {
        var store = new InMemoryTitlesStore();
        var titleDefinitionId = TitleDefinitionId.New();
        store.Titles.Unlock(titleDefinitionId);
        store.Titles.SetCurrent(titleDefinitionId);
        var useCase = new ClearCurrentCommunityTitle(store);

        var changed = await useCase.ExecuteAsync(
            store.Titles.CommunityIdentityId,
            TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Null(store.Titles.CurrentTitleDefinitionId);
        Assert.True(store.Titles.IsUnlocked(titleDefinitionId));
    }

    [Fact]
    public async Task UseCasesPassThroughTheDomainBoolNoOpResult()
    {
        var store = new InMemoryTitlesStore();
        var titleDefinitionId = TitleDefinitionId.New();
        var useCase = new UnlockCommunityTitle(store);

        Assert.True(await useCase.ExecuteAsync(
            store.Titles.CommunityIdentityId,
            titleDefinitionId,
            TestContext.Current.CancellationToken));
        Assert.False(await useCase.ExecuteAsync(
            store.Titles.CommunityIdentityId,
            titleDefinitionId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ConstructorsRejectNullStore()
    {
        Assert.Throws<ArgumentNullException>(() => new UnlockCommunityTitle(null!));
        Assert.Throws<ArgumentNullException>(() => new LockCommunityTitle(null!));
        Assert.Throws<ArgumentNullException>(() => new SetCurrentCommunityTitle(null!));
        Assert.Throws<ArgumentNullException>(() => new ClearCurrentCommunityTitle(null!));
    }

    private sealed class InMemoryTitlesStore : ICommunityTitlesStore
    {
        public InMemoryTitlesStore()
        {
            Titles = CommunityTitles.Create(CommunityIdentityId.New());
        }

        public CommunityTitles Titles { get; }

        public int CallCount { get; private set; }

        public Task<TResult> ExecuteAsync<TResult>(
            CommunityIdentityId communityIdentityId,
            Func<CommunityTitles, TResult> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(Titles.CommunityIdentityId, communityIdentityId);
            CallCount++;
            return Task.FromResult(operation(Titles));
        }
    }
}
