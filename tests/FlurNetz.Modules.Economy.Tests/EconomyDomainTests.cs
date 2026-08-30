using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;
using System.Reflection;

namespace FlurNetz.Modules.Economy.Tests;

public sealed class EconomyBalanceTests
{
    [Fact]
    public void Zero_IsValid()
    {
        Assert.Equal(0, EconomyBalance.Zero.Value);
        Assert.Equal(EconomyBalance.Zero, EconomyBalance.Create(0));
    }

    [Fact]
    public void Create_AcceptsPositiveValues()
    {
        var balance = EconomyBalance.Create(42);

        Assert.Equal(42, balance.Value);
    }

    [Fact]
    public void Create_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyBalance.Create(-1));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        Assert.Equal(EconomyBalance.Create(42), EconomyBalance.Create(42));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(EconomyBalance.Create(41), EconomyBalance.Create(42));
    }

    [Fact]
    public void Value_IsExposedWithoutASetter()
    {
        var property = typeof(EconomyBalance).GetProperty(nameof(EconomyBalance.Value));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Credit_AddsPositiveAmountWithoutMutatingTheOriginal()
    {
        var original = EconomyBalance.Create(10);

        var result = original.Credit(5);

        Assert.Equal(15, result.Value);
        Assert.Equal(10, original.Value);
    }

    [Fact]
    public void Credit_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyBalance.Zero.Credit(0));
    }

    [Fact]
    public void Credit_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyBalance.Zero.Credit(-1));
    }

    [Fact]
    public void Credit_RejectsOverflow()
    {
        var balance = EconomyBalance.Create(long.MaxValue);

        Assert.Throws<OverflowException>(() => balance.Credit(1));
    }

    [Fact]
    public void Debit_SubtractsPositiveAmountWithoutMutatingTheOriginal()
    {
        var original = EconomyBalance.Create(10);

        var result = original.Debit(4);

        Assert.Equal(6, result.Value);
        Assert.Equal(10, original.Value);
    }

    [Fact]
    public void Debit_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyBalance.Zero.Debit(0));
    }

    [Fact]
    public void Debit_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyBalance.Zero.Debit(-1));
    }

    [Fact]
    public void Debit_AllowsReducingTheBalanceExactlyToZero()
    {
        var result = EconomyBalance.Create(5).Debit(5);

        Assert.Equal(EconomyBalance.Zero, result);
    }

    [Fact]
    public void Debit_RejectsAnInsufficientBalance()
    {
        var balance = EconomyBalance.Create(5);

        Assert.Throws<InsufficientEconomyBalanceException>(() => balance.Debit(6));
    }

    [Fact]
    public void FailedDebit_DoesNotChangeTheOriginalImmutableValue()
    {
        var original = EconomyBalance.Create(5);

        Assert.Throws<InsufficientEconomyBalanceException>(() => original.Debit(6));

        Assert.Equal(5, original.Value);
    }
}

public sealed class CommunityEconomyTests
{
    [Fact]
    public void Create_CarriesTheProvidedCommunityIdentityId()
    {
        var communityIdentityId = CommunityIdentityId.New();

        var economy = CommunityEconomy.Create(communityIdentityId);

        Assert.Equal(communityIdentityId, economy.CommunityIdentityId);
    }

    [Fact]
    public void Create_StartsWithZeroBalance()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());

        Assert.Equal(EconomyBalance.Zero, economy.Balance);
    }

    [Fact]
    public void Credit_IncreasesTheBalance()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());

        economy.Credit(25);

        Assert.Equal(25, economy.Balance.Value);
    }

    [Fact]
    public void Credit_AccumulatesMultipleCredits()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());

        economy.Credit(10);
        economy.Credit(7);

        Assert.Equal(17, economy.Balance.Value);
    }

    [Fact]
    public void Debit_ReducesTheBalance()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());
        economy.Credit(10);

        economy.Debit(4);

        Assert.Equal(6, economy.Balance.Value);
    }

    [Fact]
    public void CreditAndDebit_CanBeCombined()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());

        economy.Credit(20);
        economy.Debit(8);
        economy.Credit(3);

        Assert.Equal(15, economy.Balance.Value);
    }

    [Fact]
    public void Debit_CanReduceTheBalanceExactlyToZero()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());
        economy.Credit(5);

        economy.Debit(5);

        Assert.Equal(EconomyBalance.Zero, economy.Balance);
    }

    [Fact]
    public void Debit_RejectsOverdraft()
    {
        var economy = CommunityEconomy.Create(CommunityIdentityId.New());
        economy.Credit(5);

        Assert.Throws<InsufficientEconomyBalanceException>(() => economy.Debit(6));
        Assert.Equal(5, economy.Balance.Value);
    }

    [Fact]
    public void CommunityIdentityId_IsImmutable()
    {
        var property = typeof(CommunityEconomy).GetProperty(nameof(CommunityEconomy.CommunityIdentityId));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Balance_HasNoPublicSetter()
    {
        var property = typeof(CommunityEconomy).GetProperty(nameof(CommunityEconomy.Balance));

        Assert.NotNull(property);
        Assert.Null(property!.GetSetMethod());
    }

    [Fact]
    public void Create_RejectsAnInvalidCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityEconomy.Create(default));
    }

    [Fact]
    public void Create_HasNoPublicParameterlessConstructor()
    {
        Assert.Empty(typeof(CommunityEconomy).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }
}
