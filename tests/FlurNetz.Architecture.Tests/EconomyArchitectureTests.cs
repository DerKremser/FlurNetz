using System.Data.Common;
using System.Reflection;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Architecture.Tests;

public sealed class EconomyArchitectureTests
{
    private static Assembly EconomyContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy.Contracts");

    [Fact]
    public void EconomyImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetFlurNetzReferences(
            ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy"));
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void EconomyDomainTypesAreNotPublishedThroughEconomyContracts()
    {
        var implementation = ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy");

        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.EconomyBalance"));
        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.CommunityEconomy"));
        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.InsufficientEconomyBalanceException"));
        Assert.DoesNotContain(
            EconomyContractsAssembly.GetExportedTypes(),
            type => type.Namespace == "FlurNetz.Modules.Economy.Domain");
    }

    [Fact]
    public void EconomyContractsReferenceOnlyIdentityContracts()
    {
        Assert.Equal(
            ["FlurNetz.Modules.Identity.Contracts"],
            GetFlurNetzReferences(EconomyContractsAssembly));
    }

    [Fact]
    public void EconomyContractsExposeOnlyTransactionAwareCreditAndDebitCapabilities()
    {
        var exportedTypes = EconomyContractsAssembly.GetExportedTypes().ToHashSet();

        Assert.True(exportedTypes.SetEquals(
        [
            typeof(IEconomyBalanceCredit),
            typeof(IEconomyBalanceDebit)
        ]));

        AssertTransactionAwareCapability(typeof(IEconomyBalanceCredit), nameof(IEconomyBalanceCredit.CreditAsync));
        AssertTransactionAwareCapability(typeof(IEconomyBalanceDebit), nameof(IEconomyBalanceDebit.DebitAsync));
        Assert.DoesNotContain(exportedTypes, type =>
            type.Name.Contains("Reward", StringComparison.Ordinal)
            || type.Name.Contains("Shop", StringComparison.Ordinal));
    }

    [Fact]
    public void EconomyStoreExposesMatchingTransactionAwareCreditAndDebitOperations()
    {
        AssertStoreOperation(nameof(ICommunityEconomyStore.CreditAsync));
        AssertStoreOperation(nameof(ICommunityEconomyStore.DebitAsync));
    }

    private static void AssertTransactionAwareCapability(Type contractType, string methodName)
    {
        var method = contractType.GetMethod(
            methodName,
            [
                typeof(CommunityIdentityId),
                typeof(long),
                typeof(DbConnection),
                typeof(DbTransaction),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    private static void AssertStoreOperation(string methodName)
    {
        var method = typeof(ICommunityEconomyStore).GetMethod(
            methodName,
            [
                typeof(CommunityIdentityId),
                typeof(long),
                typeof(DbConnection),
                typeof(DbTransaction),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<EconomyBalance>), method!.ReturnType);
    }

    private static string[] GetFlurNetzReferences(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
