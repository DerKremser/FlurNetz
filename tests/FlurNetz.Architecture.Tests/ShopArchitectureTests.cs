using System.Reflection;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert Assembly-, Typ-, Persistenz- und DI-Grenzen des Shop-Katalog-Slices.
/// </summary>
public sealed class ShopArchitectureTests
{
    private static Assembly ShopImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop");

    private static Assembly ShopContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop.Contracts");

    [Fact]
    public void ShopImplementationReferencesOnlyShopInventoryContractsAndPersistence()
    {
        var references = GetReferencedAssemblyNames(ShopImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Shop.Contracts",
            "FlurNetz.Modules.Inventory.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Shop.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Inventory.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Modules.Administration", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void ShopContractsReferenceNoFlurNetzAssemblies()
    {
        Assert.Empty(GetReferencedAssemblyNames(ShopContractsAssembly));
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
    public void ShopDomainAndCatalogTypesRemainInTheImplementationAssembly()
    {
        var expectedTypes = new[]
        {
            typeof(ShopOffer),
            typeof(ShopPrice),
            typeof(AvailabilityWindow),
            typeof(IShopOfferStore),
            typeof(ShopOfferNotFoundException),
            typeof(CreateShopOffer),
            typeof(GetShopOffer),
            typeof(ListShopOffers),
            typeof(RenameShopOffer),
            typeof(ChangeShopOfferDescription),
            typeof(ChangeShopOfferPrice),
            typeof(ChangeShopOfferAvailability),
            typeof(ChangeShopOfferPurchaseLimit),
            typeof(EnableShopOffer),
            typeof(DisableShopOffer),
            typeof(ShopOfferStore),
            typeof(ShopMigrationSource),
            typeof(ShopModule)
        };

        foreach (var expectedType in expectedTypes)
        {
            Assert.Equal(ShopImplementationAssembly, expectedType.Assembly);
            Assert.Null(ShopContractsAssembly.GetType(expectedType.FullName!));
        }
    }

    [Fact]
    public void ShopOfferExposesControlledRehydrationWithinTheDomainAssembly()
    {
        var method = typeof(ShopOffer).GetMethod(
            nameof(ShopOffer.Rehydrate),
            BindingFlags.Public | BindingFlags.Static,
            [
                typeof(ShopOfferId),
                typeof(ItemDefinitionId),
                typeof(string),
                typeof(string),
                typeof(ShopPrice),
                typeof(bool),
                typeof(AvailabilityWindow),
                typeof(int?)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(ShopOffer), method!.ReturnType);
        Assert.Equal(typeof(ShopOffer), method.DeclaringType);
        Assert.Equal(ShopImplementationAssembly, method.DeclaringType!.Assembly);
        Assert.Null(typeof(ShopOffer).GetProperty(nameof(ShopOffer.Id))!.GetSetMethod());
        Assert.Null(typeof(ShopOffer).GetProperty(nameof(ShopOffer.ItemDefinitionId))!.GetSetMethod());
    }

    [Fact]
    public void ShopStoreMutationBoundaryIsNonGenericAndSynchronous()
    {
        var method = typeof(IShopOfferStore).GetMethod(nameof(IShopOfferStore.ExecuteAsync));

        Assert.NotNull(method);
        Assert.False(method!.IsGenericMethod);
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
        Assert.Equal(
            typeof(Func<ShopOffer, bool>),
            method.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void ShopMigrationV1OwnsOnlyShopOffersWithRequiredIdentity()
    {
        var migrations = new ShopMigrationSource().GetMigrations().ToArray();

        var migration = Assert.Single(migrations);
        Assert.Equal("Shop", migration.Owner);
        Assert.Equal(1, migration.Version);
        Assert.Equal("CreateShopOffers", migration.Name);
        Assert.Contains("CREATE TABLE IF NOT EXISTS shop_offers", migration.Sql);
        Assert.DoesNotContain("shop_purchases", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("shop_purchase_guards", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("shop_purchase_requests", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("price bigint NOT NULL", migration.Sql);
        Assert.Contains("is_enabled boolean NOT NULL", migration.Sql);
        Assert.Contains("available_from timestamptz NULL", migration.Sql);
        Assert.Contains("available_until timestamptz NULL", migration.Sql);
        Assert.Contains("purchase_limit_per_identity integer NULL", migration.Sql);
    }

    [Fact]
    public void ShopModuleRegistersOnlyTheCurrentCatalogSlice()
    {
        var services = new ServiceCollection();

        var result = services.AddShopModule();

        Assert.Same(services, result);
        Assert.Equal(12, services.Count);
        AssertService<IShopOfferStore, ShopOfferStore>(services, ServiceLifetime.Scoped);
        AssertService<CreateShopOffer, CreateShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<GetShopOffer, GetShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<ListShopOffers, ListShopOffers>(services, ServiceLifetime.Scoped);
        AssertService<RenameShopOffer, RenameShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferDescription, ChangeShopOfferDescription>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferPrice, ChangeShopOfferPrice>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferAvailability, ChangeShopOfferAvailability>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferPurchaseLimit, ChangeShopOfferPurchaseLimit>(services, ServiceLifetime.Scoped);
        AssertService<EnableShopOffer, EnableShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<DisableShopOffer, DisableShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<IMigrationSource, ShopMigrationSource>(services, ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.FullName?.Contains("Clock", StringComparison.OrdinalIgnoreCase) == true
            || descriptor.ServiceType.FullName?.Contains("Message", StringComparison.OrdinalIgnoreCase) == true
            || (descriptor.ServiceType.FullName?.Contains("Purchase", StringComparison.OrdinalIgnoreCase) == true
                && descriptor.ServiceType != typeof(ChangeShopOfferPurchaseLimit)));
    }

    [Fact]
    public void ShopDomainAndApplicationApisDoNotLeakTechnicalPersistenceTypes()
    {
        var fachlicheTypes = ShopImplementationAssembly
            .GetTypes()
            .Where(type => type.Namespace is "FlurNetz.Modules.Shop.Domain"
                or "FlurNetz.Modules.Shop.Application")
            .ToArray();

        var leakedTypes = fachlicheTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(method => new[]
            {
                method.ReturnType,
                method.DeclaringType!
            }.Concat(method.GetParameters().Select(parameter => parameter.ParameterType)))
            .Where(type => type.FullName is not null
                && (type.FullName.Contains("Dapper", StringComparison.Ordinal)
                    || type.FullName.Contains("Npgsql", StringComparison.Ordinal)
                    || type.FullName.Contains("FlurNetz.Persistence", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leakedTypes);
    }

    [Fact]
    public void ShopContainsNoPrematurePurchaseMessagingOrGenericRepositoryTypes()
    {
        var forbiddenNameParts = new[]
        {
            "Purchase",
            "Message",
            "Event",
            "Economy",
            "Administration",
            "Api",
            "Grant",
            "Repository",
            "Identity",
            "Reward",
            "Title",
            "Achievement",
            "Worker"
        };

        var forbiddenTypes = ShopImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Where(type => type != typeof(ChangeShopOfferPurchaseLimit))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static void AssertService<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
        where TImplementation : TService
    {
        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
