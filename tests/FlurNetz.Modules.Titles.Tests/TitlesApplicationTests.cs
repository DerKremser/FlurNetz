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

public sealed class TitleDefinitionsApplicationTests
{
    [Fact]
    public async Task CreateUsesStoreWithANewDefinitionAndReturnsItsId()
    {
        var store = new InMemoryTitleDefinitionStore();
        var useCase = new CreateTitleDefinition(store);
        var cancellationToken = new CancellationTokenSource().Token;

        var id = await useCase.ExecuteAsync(
            "  Veteran  ",
            "  Beschreibung  ",
            cancellationToken);

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(id, store.AddedDefinition!.Id);
        Assert.Equal("Veteran", store.AddedDefinition.DisplayName);
        Assert.Equal("Beschreibung", store.AddedDefinition.Description);
        Assert.Equal(1, store.AddCallCount);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task RenameUsesTheCorrectIdAndDomainCallback()
    {
        var id = TitleDefinitionId.New();
        var store = new InMemoryTitleDefinitionStore
        {
            Definition = TitleDefinition.Create(id, "Veteran", "Beschreibung")
        };
        var useCase = new RenameTitleDefinition(store);
        var cancellationToken = new CancellationTokenSource().Token;

        var changed = await useCase.ExecuteAsync(
            id,
            "Champion",
            cancellationToken);

        Assert.True(changed);
        Assert.Equal(id, store.LastExecutedId);
        Assert.Equal("Champion", store.Definition!.DisplayName);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task ChangeDescriptionUsesTheCorrectIdAndDomainCallback()
    {
        var id = TitleDefinitionId.New();
        var store = new InMemoryTitleDefinitionStore
        {
            Definition = TitleDefinition.Create(id, "Veteran", "Alt")
        };
        var useCase = new ChangeTitleDescription(store);
        var cancellationToken = new CancellationTokenSource().Token;

        var changed = await useCase.ExecuteAsync(
            id,
            "Neu",
            cancellationToken);

        Assert.True(changed);
        Assert.Equal(id, store.LastExecutedId);
        Assert.Equal("Neu", store.Definition!.Description);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task GetPassesThroughTheStoreResultAndToken()
    {
        var definition = TitleDefinition.Create(
            TitleDefinitionId.New(),
            "Veteran",
            null);
        var store = new InMemoryTitleDefinitionStore
        {
            Definition = definition
        };
        var useCase = new GetTitleDefinition(store);
        var cancellationToken = new CancellationTokenSource().Token;

        var result = await useCase.ExecuteAsync(
            definition.Id,
            cancellationToken);

        Assert.Same(definition, result);
        Assert.Equal(definition.Id, store.LastGetId);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public async Task GetPassesThroughNull()
    {
        var store = new InMemoryTitleDefinitionStore();
        var useCase = new GetTitleDefinition(store);

        var result = await useCase.ExecuteAsync(
            TitleDefinitionId.New(),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListPassesThroughTheReadOnlyStoreResult()
    {
        var first = TitleDefinition.Create(TitleDefinitionId.New(), "A", null);
        var second = TitleDefinition.Create(TitleDefinitionId.New(), "B", null);
        IReadOnlyList<TitleDefinition> expected = Array.AsReadOnly([first, second]);
        var store = new InMemoryTitleDefinitionStore
        {
            ListResult = expected
        };
        var useCase = new ListTitleDefinitions(store);
        var cancellationToken = new CancellationTokenSource().Token;

        var result = await useCase.ExecuteAsync(cancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(cancellationToken, store.LastCancellationToken);
    }

    [Fact]
    public void CatalogUseCaseConstructorsRejectNullStore()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateTitleDefinition(null!));
        Assert.Throws<ArgumentNullException>(() => new RenameTitleDefinition(null!));
        Assert.Throws<ArgumentNullException>(() => new ChangeTitleDescription(null!));
        Assert.Throws<ArgumentNullException>(() => new GetTitleDefinition(null!));
        Assert.Throws<ArgumentNullException>(() => new ListTitleDefinitions(null!));
    }

    [Fact]
    public void TitleDefinitionNotFoundExceptionKeepsItsValidId()
    {
        var id = TitleDefinitionId.New();

        var exception = new TitleDefinitionNotFoundException(id);

        Assert.Equal(id, exception.TitleDefinitionId);
        Assert.Contains(id.Value.ToString(), exception.Message);
        Assert.Contains("nicht gefunden", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TitleDefinitionNotFoundExceptionRejectsDefaultId()
    {
        Assert.Throws<ArgumentException>(() => new TitleDefinitionNotFoundException(default));
    }

    private sealed class InMemoryTitleDefinitionStore : ITitleDefinitionStore
    {
        public TitleDefinition? Definition { get; init; }

        public IReadOnlyList<TitleDefinition> ListResult { get; init; } =
            Array.AsReadOnly(Array.Empty<TitleDefinition>());

        public TitleDefinition? AddedDefinition { get; private set; }

        public int AddCallCount { get; private set; }

        public TitleDefinitionId? LastExecutedId { get; private set; }

        public TitleDefinitionId? LastGetId { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task AddAsync(
            TitleDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            LastCancellationToken = cancellationToken;
            AddedDefinition = definition;
            AddCallCount++;
            return Task.CompletedTask;
        }

        public Task<TitleDefinition?> GetAsync(
            TitleDefinitionId titleDefinitionId,
            CancellationToken cancellationToken = default)
        {
            LastGetId = titleDefinitionId;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Definition);
        }

        public Task<IReadOnlyList<TitleDefinition>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(ListResult);
        }

        public Task<TResult> ExecuteAsync<TResult>(
            TitleDefinitionId titleDefinitionId,
            Func<TitleDefinition, TResult> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            LastExecutedId = titleDefinitionId;
            LastCancellationToken = cancellationToken;

            if (Definition is null)
            {
                throw new InvalidOperationException("Test definition is missing.");
            }

            return Task.FromResult(operation(Definition));
        }
    }
}
