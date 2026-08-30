using System.Reflection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den bewusst kleinen Umfang und die Abhängigkeitsgrenze der Inventory-Foundation.
/// </summary>
public sealed class InventoryArchitectureTests
{
    private static Assembly InventoryImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Inventory");

    private static Assembly InventoryContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Inventory.Contracts");

    [Fact]
    public void InventoryImplementationReferencesOnlyItsContractsAndIdentityContracts()
    {
        var references = GetReferencedAssemblyNames(InventoryImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Inventory.Contracts",
            "FlurNetz.Modules.Identity.Contracts"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void InventoryContractsRemainEmpty()
    {
        Assert.Empty(InventoryContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void InventoryDomainTypesRemainInTheImplementationAssembly()
    {
        var expectedTypeNames = new[]
        {
            "FlurNetz.Modules.Inventory.Domain.ItemDefinitionId",
            "FlurNetz.Modules.Inventory.Domain.InventoryQuantity",
            "FlurNetz.Modules.Inventory.Domain.InsufficientInventoryQuantityException",
            "FlurNetz.Modules.Inventory.Domain.CommunityInventoryEntry"
        };

        foreach (var typeName in expectedTypeNames)
        {
            Assert.NotNull(InventoryImplementationAssembly.GetType(typeName));
            Assert.Null(InventoryContractsAssembly.GetType(typeName));
        }
    }

    [Fact]
    public void InventoryFoundationContainsNoOutOfScopeProductiveTypes()
    {
        var forbiddenNameParts = new[]
        {
            "Repository",
            "Store",
            "Migration",
            "Event",
            "Message",
            "Inbox",
            "Outbox",
            "Reward",
            "Shop",
            "Product",
            "Purchase",
            "Price",
            "Cost",
            "Service",
            "Handler",
            "UseCase",
            "Grant"
        };

        var forbiddenTypes = InventoryImplementationAssembly
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
