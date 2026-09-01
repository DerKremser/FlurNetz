using System.Data.Common;
using System.Reflection;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

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
    public void InventoryContractsReferenceOnlyIdentityContracts()
    {
        Assert.Equal(
            ["FlurNetz.Modules.Identity.Contracts"],
            GetReferencedAssemblyNames(InventoryContractsAssembly));
    }

    [Fact]
    public void InventoryContractsContainOnlyItemDefinitionIdAndNeutralGrantCapability()
    {
        var exportedTypes = InventoryContractsAssembly.GetExportedTypes().ToHashSet();

        Assert.True(exportedTypes.SetEquals(
        [
            typeof(ItemDefinitionId),
            typeof(IInventoryQuantityGrant)
        ]));

        var method = typeof(IInventoryQuantityGrant).GetMethod(
            nameof(IInventoryQuantityGrant.GrantAsync),
            [
                typeof(CommunityIdentityId),
                typeof(ItemDefinitionId),
                typeof(long),
                typeof(DbConnection),
                typeof(DbTransaction),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void InventoryDomainAndImplementationTypesRemainOwnedByInventory()
    {
        var domainTypeNames = new[]
        {
            "FlurNetz.Modules.Inventory.Domain.InventoryQuantity",
            "FlurNetz.Modules.Inventory.Domain.InsufficientInventoryQuantityException",
            "FlurNetz.Modules.Inventory.Domain.CommunityInventoryEntry"
        };

        foreach (var typeName in domainTypeNames)
        {
            Assert.NotNull(InventoryImplementationAssembly.GetType(typeName));
            Assert.Null(InventoryContractsAssembly.GetType(typeName));
        }

        var implementationTypes = new[]
        {
            typeof(ICommunityInventoryStore),
            typeof(CommunityInventoryStore),
            typeof(InventoryMigrationSource),
            typeof(AddInventoryQuantity),
            typeof(RemoveInventoryQuantity),
            typeof(InventoryQuantityGrant)
        };

        Assert.All(implementationTypes, type => Assert.Equal(InventoryImplementationAssembly, type.Assembly));
    }

    [Fact]
    public void InventoryGrantCapabilityRegistersOnlyGrantRuntimeAndMigration()
    {
        var services = new ServiceCollection();

        var result = services.AddInventoryGrantCapability();

        Assert.Same(services, result);
        Assert.Equal(3, services.Count);
        AssertService<ICommunityInventoryStore, CommunityInventoryStore>(services, ServiceLifetime.Scoped);
        AssertService<IInventoryQuantityGrant, InventoryQuantityGrant>(services, ServiceLifetime.Scoped);
        AssertService<IMigrationSource, InventoryMigrationSource>(services, ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(AddInventoryQuantity)
                || descriptor.ServiceType == typeof(RemoveInventoryQuantity));
    }

    [Fact]
    public void InventoryModuleKeepsItsCompleteRuntimeComposition()
    {
        var services = new ServiceCollection();

        var result = services.AddInventoryModule();

        Assert.Same(services, result);
        Assert.Equal(5, services.Count);
        AssertService<ICommunityInventoryStore, CommunityInventoryStore>(services, ServiceLifetime.Scoped);
        AssertService<IInventoryQuantityGrant, InventoryQuantityGrant>(services, ServiceLifetime.Scoped);
        AssertService<AddInventoryQuantity, AddInventoryQuantity>(services, ServiceLifetime.Scoped);
        AssertService<RemoveInventoryQuantity, RemoveInventoryQuantity>(services, ServiceLifetime.Scoped);
        AssertService<IMigrationSource, InventoryMigrationSource>(services, ServiceLifetime.Singleton);
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
            "Cost"
        };

        var forbiddenTypes = InventoryImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.DoesNotContain(forbiddenTypes, _ => true);
    }

    [Fact]
    public void InventoryContainsNoGenericRepositoryOrExternalPlatformTypes()
    {
        Assert.DoesNotContain(InventoryImplementationAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split((char)96)[0] is "IRepository" or "Repository" or "GenericRepository"),
            _ => true);

        Assert.DoesNotContain(InventoryImplementationAssembly
            .GetTypes()
            .Where(type => ModuleArchitectureCatalog.ExternalPlatformNames.Any(platformName =>
                type.Name.StartsWith(platformName, StringComparison.Ordinal))),
            _ => true);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static void AssertService<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
        where TImplementation : TService
    {
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }
}
