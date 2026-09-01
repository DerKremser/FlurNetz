using System.Data.Common;
using System.Reflection;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Modules.Shop;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

public sealed class ShopArchitectureTests
{
    private static Assembly ShopImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop");

    private static Assembly ShopContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Shop.Contracts");

    [Fact]
    public void ShopImplementationReferencesOnlyApprovedTechnicalAssembliesAndForeignContracts()
    {
        var references = GetReferencedAssemblyNames(ShopImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.BuildingBlocks",
            "FlurNetz.Messaging",
            "FlurNetz.Modules.Shop.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Modules.Inventory.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Shop.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Economy.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Inventory.Contracts", references);
        Assert.Contains("FlurNetz.Messaging", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Modules.Inventory", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void ShopContractsReferenceOnlyMessaging()
    {
        Assert.Equal(["FlurNetz.Messaging"], GetReferencedAssemblyNames(ShopContractsAssembly));
    }

    [Fact]
    public void ShopContractsContainOnlyStableIdentifiersAndPurchaseCompletedEvent()
    {
        var exportedTypes = ShopContractsAssembly.GetExportedTypes().ToHashSet();

        Assert.True(exportedTypes.SetEquals(
        [
            typeof(ShopOfferId),
            typeof(ShopPurchaseId),
            typeof(ShopPurchaseRequestId),
            typeof(ShopPurchaseCompletedIntegrationEvent)
        ]));
        Assert.True(typeof(IIntegrationEvent).IsAssignableFrom(typeof(ShopPurchaseCompletedIntegrationEvent)));
        Assert.Equal("shop.purchase-completed", ShopPurchaseCompletedIntegrationEvent.MessageType);
        Assert.Equal(1, ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
    }

    [Fact]
    public void ShopDomainCatalogAndPurchaseTypesRemainInImplementationAssembly()
    {
        var expectedTypes = new[]
        {
            typeof(ShopOffer),
            typeof(ShopPrice),
            typeof(AvailabilityWindow),
            typeof(ShopPurchase),
            typeof(IShopOfferStore),
            typeof(IShopPurchaseHistoryStore),
            typeof(IShopPurchaseExecutor),
            typeof(PurchaseShopOffer),
            typeof(GetShopPurchase),
            typeof(ListShopPurchasesForIdentity),
            typeof(ShopPurchaseHistoryCursor),
            typeof(ShopPurchaseHistoryPage),
            typeof(PostgreSqlShopPurchaseExecutor),
            typeof(ShopOfferStore),
            typeof(ShopPurchaseHistoryStore),
            typeof(ShopMigrationSource),
            typeof(ShopModule)
        };

        Assert.All(expectedTypes, type => Assert.Equal(ShopImplementationAssembly, type.Assembly));
        Assert.DoesNotContain(
            ShopContractsAssembly.GetTypes(),
            type => expectedTypes.Contains(type));
    }

    [Fact]
    public void ShopOfferStoreMutationBoundaryRemainsNonGenericAndSynchronous()
    {
        var method = typeof(IShopOfferStore).GetMethod(nameof(IShopOfferStore.ExecuteAsync));

        Assert.NotNull(method);
        Assert.False(method!.IsGenericMethod);
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
        Assert.Equal(typeof(Func<ShopOffer, bool>), method.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void ShopPurchaseApplicationBoundaryDoesNotLeakDatabaseTypes()
    {
        var method = typeof(IShopPurchaseExecutor).GetMethod(nameof(IShopPurchaseExecutor.ExecuteAsync));

        Assert.NotNull(method);
        Assert.DoesNotContain(
            method!.GetParameters(),
            parameter => parameter.ParameterType == typeof(DbConnection)
                || parameter.ParameterType == typeof(DbTransaction)
                || parameter.ParameterType.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true
                || parameter.ParameterType.FullName?.Contains("Dapper", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ShopPurchaseHistoryApplicationBoundaryDoesNotLeakDatabaseTypes()
    {
        var methods = typeof(IShopPurchaseHistoryStore).GetMethods();

        Assert.Equal(2, methods.Length);
        Assert.All(
            methods.SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)),
            parameterType =>
            {
                Assert.NotEqual(typeof(DbConnection), parameterType);
                Assert.NotEqual(typeof(DbTransaction), parameterType);
                Assert.DoesNotContain("Npgsql", parameterType.FullName ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain("Dapper", parameterType.FullName ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain("FlurNetz.Persistence", parameterType.FullName ?? string.Empty, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void ShopMigrationKeepsCatalogV1AndAddsFocusedPurchaseV2()
    {
        var migrations = new ShopMigrationSource().GetMigrations().OrderBy(m => m.Version).ToArray();

        Assert.Equal(2, migrations.Length);

        var catalog = migrations[0];
        Assert.Equal("Shop", catalog.Owner);
        Assert.Equal(1, catalog.Version);
        Assert.Equal("CreateShopOffers", catalog.Name);
        Assert.Contains("CREATE TABLE IF NOT EXISTS shop_offers", catalog.Sql);
        Assert.DoesNotContain("shop_purchases", catalog.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES", catalog.Sql, StringComparison.OrdinalIgnoreCase);

        var purchase = migrations[1];
        Assert.Equal("Shop", purchase.Owner);
        Assert.Equal(2, purchase.Version);
        Assert.Equal("CreateShopPurchases", purchase.Name);
        Assert.Contains("shop_purchase_requests", purchase.Sql, StringComparison.Ordinal);
        Assert.Contains("shop_purchase_guards", purchase.Sql, StringComparison.Ordinal);
        Assert.Contains("shop_purchases", purchase.Sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (shop_purchase_id)", purchase.Sql, StringComparison.Ordinal);
        Assert.Contains("REFERENCES shop_offers (id)", purchase.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES community_identities", purchase.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES community_inventory", purchase.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES community_economies", purchase.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShopModuleRegistersCatalogPurchaseExecutorHistoryClockAndMigration()
    {
        var services = new ServiceCollection();

        var result = services.AddShopModule();

        Assert.Same(services, result);
        Assert.Equal(18, services.Count);
        AssertService<IClock, SystemClock>(services, ServiceLifetime.Singleton);
        AssertService<IShopOfferStore, ShopOfferStore>(services, ServiceLifetime.Scoped);
        AssertService<IShopPurchaseHistoryStore, ShopPurchaseHistoryStore>(services, ServiceLifetime.Scoped);
        AssertService<IShopPurchaseExecutor, PostgreSqlShopPurchaseExecutor>(services, ServiceLifetime.Scoped);
        AssertService<PurchaseShopOffer, PurchaseShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<CreateShopOffer, CreateShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<GetShopOffer, GetShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<ListShopOffers, ListShopOffers>(services, ServiceLifetime.Scoped);
        AssertService<GetShopPurchase, GetShopPurchase>(services, ServiceLifetime.Scoped);
        AssertService<ListShopPurchasesForIdentity, ListShopPurchasesForIdentity>(services, ServiceLifetime.Scoped);
        AssertService<RenameShopOffer, RenameShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferDescription, ChangeShopOfferDescription>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferPrice, ChangeShopOfferPrice>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferAvailability, ChangeShopOfferAvailability>(services, ServiceLifetime.Scoped);
        AssertService<ChangeShopOfferPurchaseLimit, ChangeShopOfferPurchaseLimit>(services, ServiceLifetime.Scoped);
        AssertService<EnableShopOffer, EnableShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<DisableShopOffer, DisableShopOffer>(services, ServiceLifetime.Scoped);
        AssertService<IMigrationSource, ShopMigrationSource>(services, ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IIntegrationEventPublisher));
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
    public void ShopContainsNoForeignImplementationGenericRepositoryOrPrematureFeatureTypes()
    {
        var references = GetReferencedAssemblyNames(ShopImplementationAssembly);
        Assert.DoesNotContain(references, reference =>
            reference is "FlurNetz.Modules.Identity"
                or "FlurNetz.Modules.Economy"
                or "FlurNetz.Modules.Inventory"
                or "FlurNetz.Modules.Rewards"
                or "FlurNetz.Modules.Titles"
                or "FlurNetz.Modules.Achievements");

        var forbiddenNameParts = new[]
        {
            "GenericRepository",
            "UnitOfWork",
            "Cart",
            "Refund",
            "Coupon",
            "Discount",
            "Stock",
            "Administration",
            "Worker"
        };

        var forbiddenTypes = ShopImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static void AssertService<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
        where TImplementation : TService
    {
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
