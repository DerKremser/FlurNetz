using System.Data.Common;
using System.Reflection;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications;
using FlurNetz.Modules.Notifications.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Modules.Notifications.Migrations;
using FlurNetz.Modules.Notifications.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert Ownership, Abhängigkeiten und die bewusst kleine V1-Grenze von Notifications.
/// </summary>
public sealed class NotificationsArchitectureTests
{
    private static Assembly ImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Notifications");

    private static Assembly ContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Notifications.Contracts");

    [Fact]
    public void ImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(ImplementationAssembly);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.BuildingBlocks",
            "FlurNetz.Messaging",
            "FlurNetz.Modules.Notifications.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Modules.Shop.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Messaging", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Shop.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.All(references, reference => Assert.Contains(reference, allowed));
    }

    [Fact]
    public void ContractsExposeExactlyTheNarrowCreateCapability()
    {
        Assert.True(ContractsAssembly.GetExportedTypes().ToHashSet().SetEquals([typeof(ICommunityNotificationCreate)]));
        Assert.Contains("FlurNetz.Modules.Identity.Contracts",
            GetReferencedAssemblyNames(ContractsAssembly));
        Assert.DoesNotContain(typeof(CommunityNotification).Assembly.GetExportedTypes(),
            type => type.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true
                && ContractsAssembly.GetExportedTypes().Contains(type));
    }

    [Fact]
    public void DomainApplicationAndPersistenceTypesAreOwnedByImplementationAssembly()
    {
        var expected = new[]
        {
            typeof(NotificationId),
            typeof(NotificationSourceReference),
            typeof(CommunityNotification),
            typeof(ICommunityNotificationStore),
            typeof(NotificationInboxCursor),
            typeof(CommunityNotificationPage),
            typeof(CreateNotification),
            typeof(CommunityNotificationCreateCapability),
            typeof(GetNotification),
            typeof(ListNotificationsForIdentity),
            typeof(GetUnreadNotificationCount),
            typeof(MarkNotificationRead),
            typeof(MarkNotificationUnread),
            typeof(MarkAllNotificationsRead),
            typeof(ShopPurchaseCompletedIntegrationEventHandler),
            typeof(CommunityNotificationStore),
            typeof(NotificationsMigrationSource),
            typeof(NotificationsModule)
        };

        Assert.All(expected, type => Assert.Equal(ImplementationAssembly, type.Assembly));
    }

    [Fact]
    public void ConsumerIsExplicitAndOnlyConsumesShopContract()
    {
        Assert.Contains(
            typeof(IIntegrationEventHandler<FlurNetz.Modules.Shop.Contracts.ShopPurchaseCompletedIntegrationEvent>),
            typeof(ShopPurchaseCompletedIntegrationEventHandler).GetInterfaces());
        Assert.Equal(
            "notifications.shop-purchase",
            ShopPurchaseCompletedIntegrationEventHandler.ConsumerName);
        Assert.DoesNotContain(
            ImplementationAssembly.GetExportedTypes(),
            type => typeof(IIntegrationEvent).IsAssignableFrom(type)
                && !type.IsInterface);
    }

    [Fact]
    public void TransactionAwareStoreBoundaryUsesOnlyNeutralAdoNetTypes()
    {
        var method = typeof(ICommunityNotificationStore).GetMethod(
            nameof(ICommunityNotificationStore.AddAsync),
            [typeof(CommunityNotification), typeof(DbConnection), typeof(DbTransaction), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), parameter =>
            parameter.ParameterType.FullName?.Contains("Dapper", StringComparison.Ordinal) == true
                || parameter.ParameterType.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void MigrationOwnsOnlyNotificationsAndHasNoForeignKeysOrCrossModuleSql()
    {
        var migration = Assert.Single(new NotificationsMigrationSource().GetMigrations());

        Assert.Equal("Notifications", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateCommunityNotifications", migration.Name);
        Assert.Contains("community_notifications", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shop_", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModuleRegistrationHasNoConsumerUntilWorkerAddsIt()
    {
        var services = new ServiceCollection();

        services.AddNotificationsModule();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityNotificationStore)
                && descriptor.ImplementationType == typeof(CommunityNotificationStore)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMigrationSource)
                && descriptor.ImplementationType == typeof(NotificationsMigrationSource)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IIntegrationEventHandlerRegistration));

        services.AddNotificationsConsumer();
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIntegrationEventHandlerRegistration));
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
