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
        Assert.Empty(contracts.GetExportedTypes());
    }
}
