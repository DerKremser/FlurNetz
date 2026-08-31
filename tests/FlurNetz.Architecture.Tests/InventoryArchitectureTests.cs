using System.Reflection;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert Scope, Typ-Ownership und Abhängigkeitsgrenzen des persistierten Inventory-Slices.
/// </summary>
public sealed class InventoryArchitectureTests
{
    private static Assembly InventoryImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Inventory");

    private static Assembly InventoryContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Inventory.Contracts");

    [Fact]
    public void InventoryImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(InventoryImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Inventory.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void InventoryContractsContainOnlyTheRequiredPublicItemDefinitionId()
    {
        var exportedTypes = InventoryContractsAssembly.GetExportedTypes();

        var itemDefinitionId = Assert.Single(exportedTypes);
        Assert.Equal(typeof(ItemDefinitionId), itemDefinitionId);
        Assert.Equal(InventoryContractsAssembly, itemDefinitionId.Assembly);
    }

    [Fact]
    public void InventoryDomainTypesRemainInTheImplementationAssembly()
    {
        var expectedTypeNames = new[]
        {
            "FlurNetz.Modules.Inventory.Domain.InventoryQuantity",
            "FlurNetz.Modules.Inventory.Domain.InsufficientInventoryQuantityException",
            "FlurNetz.Modules.Inventory.Domain.CommunityInventoryEntry"
        };

        foreach (var typeName in expectedTypeNames)
        {
            Assert.NotNull(InventoryImplementationAssembly.GetType(typeName));
            Assert.Null(InventoryContractsAssembly.GetType(typeName));
        }

        Assert.Null(InventoryImplementationAssembly.GetType("FlurNetz.Modules.Inventory.Domain.ItemDefinitionId"));
    }

    [Fact]
    public void InventoryPersistenceAndApplicationTypesRemainInImplementationAssembly()
    {
        Assert.Equal(InventoryImplementationAssembly, typeof(ICommunityInventoryStore).Assembly);
        Assert.Equal(InventoryImplementationAssembly, typeof(CommunityInventoryStore).Assembly);
        Assert.Equal(InventoryImplementationAssembly, typeof(InventoryMigrationSource).Assembly);
        Assert.Equal(InventoryImplementationAssembly, typeof(AddInventoryQuantity).Assembly);
        Assert.Equal(InventoryImplementationAssembly, typeof(RemoveInventoryQuantity).Assembly);

        Assert.DoesNotContain(typeof(ICommunityInventoryStore), InventoryContractsAssembly.GetTypes());
        Assert.DoesNotContain(typeof(CommunityInventoryStore), InventoryContractsAssembly.GetTypes());
        Assert.DoesNotContain(typeof(AddInventoryQuantity), InventoryContractsAssembly.GetTypes());
        Assert.DoesNotContain(typeof(RemoveInventoryQuantity), InventoryContractsAssembly.GetTypes());
    }

    [Fact]
    public void InventoryMigrationOwnsOnlyItsTableAndHasNoCrossModuleSqlDependency()
    {
        var migration = Assert.Single(new InventoryMigrationSource().GetMigrations());

        Assert.Equal("Inventory", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateCommunityInventoryEntries", migration.Name);
        Assert.Contains("community_inventory_entries", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reward_", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shop", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InventoryContainsNoMessagingRewardsOrShopProductTypes()
    {
        var forbiddenNameParts = new[]
        {
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

    [Fact]
    public void InventoryContainsNoGenericRepositoryTypes()
    {
        var forbiddenTypes = InventoryImplementationAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void InventoryContainsNoExternalPlatformIdentityTypes()
    {
        var forbiddenTypes = InventoryImplementationAssembly
            .GetTypes()
            .Where(type => ModuleArchitectureCatalog.ExternalPlatformNames.Any(platformName =>
                type.Name.StartsWith(platformName, StringComparison.Ordinal)))
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
