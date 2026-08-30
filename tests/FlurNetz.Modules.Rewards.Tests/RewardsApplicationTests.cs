using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Tests;

public sealed class CreateEconomyBalanceRewardDefinitionTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsANewDefinitionWithTheRequestedAmount()
    {
        var store = new RecordingRewardCatalogStore();
        var useCase = new CreateEconomyBalanceRewardDefinition(store);

        var definitionId = await useCase.ExecuteAsync(5, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, definitionId.Value);
        Assert.NotNull(store.Definition);
        Assert.Equal(definitionId, store.Definition!.Id);
        Assert.Equal(5, store.Definition.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheCancellationToken()
    {
        var store = new RecordingRewardCatalogStore();
        var useCase = new CreateEconomyBalanceRewardDefinition(store);
        using var cancellationSource = new CancellationTokenSource();

        await useCase.ExecuteAsync(7, cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, store.DefinitionCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotHideStoreErrors()
    {
        var expected = new InvalidOperationException("catalog failure");
        var store = new RecordingRewardCatalogStore { DefinitionException = expected };
        var useCase = new CreateEconomyBalanceRewardDefinition(store);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(5, TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }
}

public sealed class CreateRewardPackageTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesAndPersistsAPackageWithTheRequestedDefinitions()
    {
        var firstDefinitionId = RewardDefinitionId.New();
        var secondDefinitionId = RewardDefinitionId.New();
        var store = new RecordingRewardCatalogStore();
        var useCase = new CreateRewardPackage(store);
        using var cancellationSource = new CancellationTokenSource();

        var packageId = await useCase.ExecuteAsync(
            [firstDefinitionId, secondDefinitionId],
            cancellationSource.Token);

        Assert.NotEqual(Guid.Empty, packageId.Value);
        Assert.NotNull(store.Package);
        Assert.Equal(packageId, store.Package!.Id);
        Assert.Equal(
            [firstDefinitionId, secondDefinitionId],
            store.Package.RewardDefinitionIds);
        Assert.Equal(cancellationSource.Token, store.MissingDefinitionsCancellationToken);
        Assert.Equal(cancellationSource.Token, store.PackageCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_LeavesDomainValidationActive()
    {
        var definitionId = RewardDefinitionId.New();
        var store = new RecordingRewardCatalogStore();
        var useCase = new CreateRewardPackage(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync([definitionId, definitionId], TestContext.Current.CancellationToken));

        Assert.Null(store.Package);
        Assert.Null(store.MissingDefinitionIds);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownDefinitionsBeforePersistingThePackage()
    {
        var knownDefinitionId = RewardDefinitionId.New();
        var missingDefinitionId = RewardDefinitionId.New();
        var store = new RecordingRewardCatalogStore
        {
            MissingDefinitionIds = [missingDefinitionId]
        };
        var useCase = new CreateRewardPackage(store);

        var exception = await Assert.ThrowsAsync<RewardDefinitionNotFoundException>(
            () => useCase.ExecuteAsync(
                [knownDefinitionId, missingDefinitionId],
                TestContext.Current.CancellationToken));

        Assert.Equal([missingDefinitionId], exception.MissingDefinitionIds);
        Assert.Null(store.Package);
    }
}

public sealed class GrantRewardPackageTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsAllArguments()
    {
        var executor = new RecordingRewardPackageGrantExecutor
        {
            Outcome = RewardPackageGrantOutcome.Granted
        };
        var useCase = new GrantRewardPackage(executor);
        var packageId = RewardPackageId.New();
        var communityIdentityId = CommunityIdentityId.New();
        var source = RewardSource.Create("test", "grant-1");
        using var cancellationSource = new CancellationTokenSource();

        var outcome = await useCase.ExecuteAsync(
            packageId,
            communityIdentityId,
            source,
            cancellationSource.Token);

        Assert.Equal(RewardPackageGrantOutcome.Granted, outcome);
        Assert.Equal(packageId, executor.RewardPackageId);
        Assert.Equal(communityIdentityId, executor.CommunityIdentityId);
        Assert.Equal(source, executor.Source);
        Assert.Equal(cancellationSource.Token, executor.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAlreadyGrantedWithoutChangingTheOutcome()
    {
        var executor = new RecordingRewardPackageGrantExecutor
        {
            Outcome = RewardPackageGrantOutcome.AlreadyGranted
        };
        var useCase = new GrantRewardPackage(executor);

        var outcome = await useCase.ExecuteAsync(
            RewardPackageId.New(),
            CommunityIdentityId.New(),
            RewardSource.Create("test", "grant-1"),
            TestContext.Current.CancellationToken);

        Assert.Equal(RewardPackageGrantOutcome.AlreadyGranted, outcome);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotHideExecutorErrors()
    {
        var expected = new InvalidOperationException("grant failure");
        var executor = new RecordingRewardPackageGrantExecutor { Exception = expected };
        var useCase = new GrantRewardPackage(executor);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(
                RewardPackageId.New(),
                CommunityIdentityId.New(),
                RewardSource.Create("test", "grant-1"),
                TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }
}

internal sealed class RecordingRewardCatalogStore : IRewardCatalogStore
{
    public EconomyBalanceRewardDefinition? Definition { get; private set; }

    public RewardPackage? Package { get; private set; }

    public CancellationToken DefinitionCancellationToken { get; private set; }

    public CancellationToken MissingDefinitionsCancellationToken { get; private set; }

    public CancellationToken PackageCancellationToken { get; private set; }

    public IReadOnlyList<RewardDefinitionId>? MissingDefinitionIds { get; init; }

    public InvalidOperationException? DefinitionException { get; init; }

    public Task AddDefinitionAsync(
        EconomyBalanceRewardDefinition definition,
        CancellationToken cancellationToken = default)
    {
        DefinitionCancellationToken = cancellationToken;
        if (DefinitionException is not null)
        {
            return Task.FromException(DefinitionException);
        }

        Definition = definition;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RewardDefinitionId>> FindMissingDefinitionIdsAsync(
        IEnumerable<RewardDefinitionId> rewardDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        MissingDefinitionsCancellationToken = cancellationToken;
        return Task.FromResult<IReadOnlyList<RewardDefinitionId>>(
            MissingDefinitionIds ?? Array.Empty<RewardDefinitionId>());
    }

    public Task AddPackageAsync(
        RewardPackage package,
        CancellationToken cancellationToken = default)
    {
        PackageCancellationToken = cancellationToken;
        Package = package;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingRewardPackageGrantExecutor : IRewardPackageGrantExecutor
{
    public RewardPackageGrantOutcome Outcome { get; init; }

    public InvalidOperationException? Exception { get; init; }

    public RewardPackageId RewardPackageId { get; private set; }

    public CommunityIdentityId CommunityIdentityId { get; private set; }

    public RewardSource? Source { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public Task<RewardPackageGrantOutcome> ExecuteAsync(
        RewardPackageId rewardPackageId,
        CommunityIdentityId communityIdentityId,
        RewardSource source,
        CancellationToken cancellationToken = default)
    {
        RewardPackageId = rewardPackageId;
        CommunityIdentityId = communityIdentityId;
        Source = source;
        CancellationToken = cancellationToken;

        return Exception is null
            ? Task.FromResult(Outcome)
            : Task.FromException<RewardPackageGrantOutcome>(Exception);
    }
}
