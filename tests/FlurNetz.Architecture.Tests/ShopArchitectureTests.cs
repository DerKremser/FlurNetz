using System.Reflection;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die minimalen Assembly- und Typgrenzen des Shop-Foundation-Slices.
/// </summary>
public sealed class ShopArchitectureTests
{
    private static Assembly ShopImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop");

    private static Assembly ShopContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop.Contracts");

    [Fact]
    public void ShopImplementationReferencesOnlyShopAndInventoryContracts()
    {
        var references = GetReferencedAssemblyNames(ShopImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Shop.Contracts",
            "FlurNetz.Modules.Inventory.Contracts"
        };

        Assert.Contains("FlurNetz.Modules.Shop.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Inventory.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Modules.Administration", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void ShopContractsReferenceNoFlurNetzAssemblies()
    {
        var references = GetReferencedAssemblyNames(ShopContractsAssembly);

        Assert.Empty(references);
    }

    [Fact]
    public void ShopContractsContainOnlyShopOfferId()
    {
        var exportedTypes = ShopContractsAssembly.GetExportedTypes();

        var shopOfferId = Assert.Single(exportedTypes);
        Assert.Equal(typeof(ShopOfferId), shopOfferId);
        Assert.Equal(ShopContractsAssembly, shopOfferId.Assembly);
        Assert.Null(ShopContractsAssembly.GetType("FlurNetz.Modules.Shop.Domain.ShopOffer"));
    }

    [Fact]
    public void ShopDomainTypesRemainInTheImplementationAssembly()
    {
        var expectedTypeNames = new[]
        {
            "FlurNetz.Modules.Shop.Domain.ShopOffer",
            "FlurNetz.Modules.Shop.Domain.ShopPrice",
            "FlurNetz.Modules.Shop.Domain.AvailabilityWindow"
        };

        foreach (var typeName in expectedTypeNames)
        {
            Assert.NotNull(ShopImplementationAssembly.GetType(typeName));
            Assert.Null(ShopContractsAssembly.GetType(typeName));
        }

        Assert.Equal("FlurNetz.Modules.Inventory.Contracts", typeof(ItemDefinitionId).Assembly.GetName().Name);
    }

    [Fact]
    public void ShopContainsNoPurchasePersistenceMessagingOrForeignModuleTypes()
    {
        var forbiddenNameParts = new[]
        {
            "Purchase",
            "Persistence",
            "Message",
            "Event",
            "Economy",
            "Administration",
            "Api",
            "Grant",
            "Transaction"
        };

        var forbiddenTypes = ShopImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
