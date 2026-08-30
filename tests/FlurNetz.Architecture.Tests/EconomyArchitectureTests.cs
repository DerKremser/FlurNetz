using System.Data.Common;
using System.Reflection;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die bewusst minimale Assembly-Grenze des Economy-Vertical-Slices ab.
/// </summary>
public sealed class EconomyArchitectureTests
{
    [Fact]
    public void EconomyImplementationReferencesOnlyApprovedProjects()
    {
        var references = ModuleArchitectureCatalog
            .LoadAssembly("FlurNetz.Modules.Economy")
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
            .Select(name => name!)
            .ToArray();

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.All(
            references,
            reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void EconomyDomainTypesAreNotPublishedThroughEconomyContracts()
    {
        var implementation = ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy");
        var contracts = ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy.Contracts");

        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.EconomyBalance"));
        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.CommunityEconomy"));
        Assert.NotNull(implementation.GetType("FlurNetz.Modules.Economy.Domain.InsufficientEconomyBalanceException"));
        Assert.Null(contracts.GetType("FlurNetz.Modules.Economy.Domain.EconomyBalance"));
        Assert.Null(contracts.GetType("FlurNetz.Modules.Economy.Domain.CommunityEconomy"));
        Assert.DoesNotContain(
            contracts.GetExportedTypes(),
            type => type.Namespace == "FlurNetz.Modules.Economy.Domain");
    }

    [Fact]
    public void EconomyContractsReferenceOnlyIdentityContracts()
    {
        var references = EconomyContractsAssembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
            .Select(name => name!)
            .ToArray();

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.All(
            references,
            reference => Assert.Equal("FlurNetz.Modules.Identity.Contracts", reference));
    }

    [Fact]
    public void EconomyContractsExposeOnlyATransactionAwareCreditCapability()
    {
        var method = typeof(IEconomyBalanceCredit).GetMethod(
            nameof(IEconomyBalanceCredit.CreditAsync),
            [
                typeof(CommunityIdentityId),
                typeof(long),
                typeof(DbConnection),
                typeof(DbTransaction),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
        Assert.DoesNotContain(
            EconomyContractsAssembly.GetExportedTypes(),
            type => type.Name.Contains("Reward", StringComparison.Ordinal));
    }

    [Fact]
    public void EconomyStoreExposesATransactionAwareCreditOperation()
    {
        var method = typeof(ICommunityEconomyStore).GetMethod(
            nameof(ICommunityEconomyStore.CreditAsync),
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

    private static Assembly EconomyContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Economy.Contracts");
}
