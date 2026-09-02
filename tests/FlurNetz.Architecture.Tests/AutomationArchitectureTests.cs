using System.Reflection;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Automation;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Automation.Migrations;
using FlurNetz.Modules.Automation.Persistence;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Notifications.Contracts;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>Sichert die schmale Ownership- und Composition-Grenze von Automation V1.</summary>
public sealed class AutomationArchitectureTests
{
    private static Assembly ImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Automation");

    private static Assembly ContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Automation.Contracts");

    [Fact]
    public void ImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(ImplementationAssembly);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Automation.Contracts",
            "FlurNetz.BuildingBlocks",
            "FlurNetz.Persistence",
            "FlurNetz.Messaging",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Modules.Engagement.Contracts",
            "FlurNetz.Modules.Shop.Contracts",
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Modules.Notifications.Contracts"
        };

        Assert.All(references, reference => Assert.Contains(reference, allowed));
        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Modules.Engagement", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Modules.Notifications", references);
    }

    [Fact]
    public void AutomationContractsRemainEmpty()
    {
        Assert.Empty(ContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void RuntimeUsesOnlyNeutralCapabilities()
    {
        var execute = typeof(ExecuteAutomationTrigger).GetConstructors().Single();
        var parameterTypes = execute.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Contains(typeof(IEconomyBalanceCredit), parameterTypes);
        Assert.Contains(typeof(ICommunityNotificationCreate), parameterTypes);
        Assert.DoesNotContain(parameterTypes, parameterType =>
            parameterType.FullName?.Contains("CommunityEconomyStore", StringComparison.Ordinal) == true
            || parameterType.FullName?.Contains("CommunityNotificationStore", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ConsumersAreExplicitAndUseTheRequiredStableNames()
    {
        Assert.Contains(
            typeof(IIntegrationEventHandler<FlurNetz.Modules.Engagement.Contracts.MessageEngagementRecordedIntegrationEvent>),
            typeof(EngagementMessageRecordedAutomationConsumer).GetInterfaces());
        Assert.Contains(
            typeof(IIntegrationEventHandler<FlurNetz.Modules.Shop.Contracts.ShopPurchaseCompletedIntegrationEvent>),
            typeof(ShopPurchaseCompletedAutomationConsumer).GetInterfaces());
        Assert.Equal("automation.engagement-message-recorded", EngagementMessageRecordedAutomationConsumer.ConsumerName);
        Assert.Equal("automation.shop-purchase-completed", ShopPurchaseCompletedAutomationConsumer.ConsumerName);
    }

    [Fact]
    public void MigrationOwnsOnlyAutomationTablesAndInternalForeignKeys()
    {
        var migration = Assert.Single(new AutomationMigrationSource().GetMigrations());

        Assert.Equal("Automation", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateAutomationRulesAndExecutions", migration.Name);
        Assert.Contains("automation_rules", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("automation_rule_conditions", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("automation_rule_actions", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("automation_executions", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES shop_", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_notifications", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_economies", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModuleRegistrationSeparatesManagementAndConsumers()
    {
        var services = new ServiceCollection();

        services.AddAutomationModule();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAutomationRuleStore)
            && descriptor.ImplementationType == typeof(PostgreSqlAutomationRuleStore)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAutomationRuntimeStore)
            && descriptor.ImplementationType == typeof(PostgreSqlAutomationRuntimeStore)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMigrationSource)
            && descriptor.ImplementationType == typeof(AutomationMigrationSource)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IIntegrationEventHandlerRegistration));

        services.AddAutomationConsumers();
        Assert.Equal(2, services.Count(descriptor =>
            descriptor.ServiceType == typeof(IIntegrationEventHandlerRegistration)));
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
