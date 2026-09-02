using System.Data.Common;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Economy;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Economy.Tests;

public sealed class CreditEconomyBalanceTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsIdentityAmountAndCancellationTokenAndReturnsNewBalance()
    {
        var store = new RecordingEconomyStore(EconomyBalance.Create(12));
        var useCase = new CreditEconomyBalance(store);
        var communityIdentityId = CommunityIdentityId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(communityIdentityId, 7, cancellationSource.Token);

        Assert.Equal(EconomyBalance.Create(12), result);
        Assert.Equal(communityIdentityId, store.CommunityIdentityId);
        Assert.Equal(7, store.Amount);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
        Assert.Equal(StoreOperation.Credit, store.Operation);
    }
}

public sealed class EconomyCapabilityRegistrationTests
{
    [Fact]
    public async Task CreditCapabilityIsResolvableAndComposesWithDebitWithoutDuplicateCoreServices()
    {
        var services = new ServiceCollection();

        services.AddEconomyDebitCapability();
        services.AddEconomyCreditCapability();
        services.AddSingleton<IPostgreSqlConnectionFactory>(_ =>
            new PostgreSqlConnectionFactory(new PostgreSqlOptions(
                "Host=localhost;Database=flurnetz-capability-test;Username=test;Password=test")));

        await using var provider = services.BuildServiceProvider();
        Assert.IsType<EconomyBalanceCredit>(provider.GetRequiredService<IEconomyBalanceCredit>());
        Assert.IsType<EconomyBalanceDebit>(provider.GetRequiredService<IEconomyBalanceDebit>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommunityEconomyStore));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IMigrationSource));
    }
}

public sealed class DebitEconomyBalanceTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsIdentityAmountAndCancellationTokenAndReturnsNewBalance()
    {
        var store = new RecordingEconomyStore(EconomyBalance.Create(5));
        var useCase = new DebitEconomyBalance(store);
        var communityIdentityId = CommunityIdentityId.New();
        using var cancellationSource = new CancellationTokenSource();

        var result = await useCase.ExecuteAsync(communityIdentityId, 3, cancellationSource.Token);

        Assert.Equal(EconomyBalance.Create(5), result);
        Assert.Equal(communityIdentityId, store.CommunityIdentityId);
        Assert.Equal(3, store.Amount);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
        Assert.Equal(StoreOperation.Debit, store.Operation);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotHideInsufficientBalanceException()
    {
        var store = new RecordingEconomyStore(EconomyBalance.Zero)
        {
            DebitException = new InsufficientEconomyBalanceException()
        };
        var useCase = new DebitEconomyBalance(store);

        await Assert.ThrowsAsync<InsufficientEconomyBalanceException>(
            () => useCase.ExecuteAsync(
                CommunityIdentityId.New(),
                1,
                TestContext.Current.CancellationToken));
    }
}

internal enum StoreOperation
{
    None,
    Credit,
    Debit
}

internal sealed class RecordingEconomyStore(EconomyBalance result) : ICommunityEconomyStore
{
    public CommunityIdentityId CommunityIdentityId { get; private set; }

    public long Amount { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public StoreOperation Operation { get; private set; }

    public InsufficientEconomyBalanceException? DebitException { get; init; }

    public Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Credit, communityIdentityId, amount, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Credit, communityIdentityId, amount, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Debit, communityIdentityId, amount, cancellationToken);
        return DebitException is null
            ? Task.FromResult(result)
            : Task.FromException<EconomyBalance>(DebitException);
    }

    public Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Record(StoreOperation.Debit, communityIdentityId, amount, cancellationToken);
        return DebitException is null
            ? Task.FromResult(result)
            : Task.FromException<EconomyBalance>(DebitException);
    }

    public Task<CommunityEconomy?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CommunityEconomy?>(null);

    private void Record(
        StoreOperation operation,
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken)
    {
        Operation = operation;
        CommunityIdentityId = communityIdentityId;
        Amount = amount;
        CancellationToken = cancellationToken;
    }
}
