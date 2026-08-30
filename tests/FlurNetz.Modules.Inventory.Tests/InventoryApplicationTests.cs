using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Domain;

namespace FlurNetz.Modules.Inventory.Tests;

public sealed class AddInventoryQuantityTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsIdentifiersAmountAndCancellationTokenAndReturnsNewQuantity()
    {
        var store = new RecordingInventoryStore(InventoryQuantity.Create(12));
        var useCase = new AddInventoryQuantity(store);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(
            communityIdentityId,
            itemDefinitionId,
            7,
            cancellationSource.Token);

        Assert.Equal(InventoryQuantity.Create(12), result);
        Assert.Equal(communityIdentityId, store.CommunityIdentityId);
        Assert.Equal(itemDefinitionId, store.ItemDefinitionId);
        Assert.Equal(7, store.Amount);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
        Assert.Equal(StoreOperation.Add, store.Operation);
    }
}

public sealed class RemoveInventoryQuantityTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsIdentifiersAmountAndCancellationTokenAndReturnsNewQuantity()
    {
        var store = new RecordingInventoryStore(InventoryQuantity.Create(5));
        var useCase = new RemoveInventoryQuantity(store);
        var communityIdentityId = CommunityIdentityId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(
            communityIdentityId,
            itemDefinitionId,
            3,
            cancellationSource.Token);

        Assert.Equal(InventoryQuantity.Create(5), result);
        Assert.Equal(communityIdentityId, store.CommunityIdentityId);
        Assert.Equal(itemDefinitionId, store.ItemDefinitionId);
        Assert.Equal(3, store.Amount);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
        Assert.Equal(StoreOperation.Remove, store.Operation);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotHideInsufficientQuantityException()
    {
        var store = new RecordingInventoryStore(InventoryQuantity.Zero)
        {
            RemoveException = new InsufficientInventoryQuantityException()
        };
        var useCase = new RemoveInventoryQuantity(store);

        await Assert.ThrowsAsync<InsufficientInventoryQuantityException>(
            () => useCase.ExecuteAsync(
                CommunityIdentityId.New(),
                ItemDefinitionId.New(),
                1,
                TestContext.Current.CancellationToken));
    }
}

internal enum StoreOperation
{
    None,
    Add,
    Remove
}

internal sealed class RecordingInventoryStore(InventoryQuantity result) : ICommunityInventoryStore
{
    public CommunityIdentityId CommunityIdentityId { get; private set; }

    public ItemDefinitionId ItemDefinitionId { get; private set; }

    public long Amount { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public StoreOperation Operation { get; private set; }

    public InsufficientInventoryQuantityException? RemoveException { get; init; }

    public Task<InventoryQuantity> AddAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Add, communityIdentityId, itemDefinitionId, amount, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<InventoryQuantity> RemoveAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Remove, communityIdentityId, itemDefinitionId, amount, cancellationToken);

        return RemoveException is null
            ? Task.FromResult(result)
            : Task.FromException<InventoryQuantity>(RemoveException);
    }

    public Task<CommunityInventoryEntry?> GetAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<CommunityInventoryEntry?>(null);
    }

    private void Record(
        StoreOperation operation,
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken)
    {
        Operation = operation;
        CommunityIdentityId = communityIdentityId;
        ItemDefinitionId = itemDefinitionId;
        Amount = amount;
        CancellationToken = cancellationToken;
    }
}
