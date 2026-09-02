using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Migrations;
using FlurNetz.Modules.Automation.Persistence;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlurNetz.Modules.Automation;

/// <summary>Registriert Management- und Runtime-Komponenten der Automation V1.</summary>
public static class AutomationModule
{
    /// <summary>Registriert Uhr, Stores, Use Cases und die Automation-Migration.</summary>
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<IAutomationRuleStore, PostgreSqlAutomationRuleStore>();
        services.TryAddScoped<IAutomationRuntimeStore, PostgreSqlAutomationRuntimeStore>();
        services.TryAddScoped<IAutomationExecutionHistoryStore, PostgreSqlAutomationExecutionHistoryStore>();
        services.TryAddScoped<CreateAutomationRule>();
        services.TryAddScoped<GetAutomationRule>();
        services.TryAddScoped<ListAutomationRules>();
        services.TryAddScoped<ReplaceAutomationRule>();
        services.TryAddScoped<EnableAutomationRule>();
        services.TryAddScoped<DisableAutomationRule>();
        services.TryAddScoped<ArchiveAutomationRule>();
        services.TryAddScoped<ListAutomationExecutions>();
        services.TryAddScoped<ExecuteAutomationTrigger>();
        services.TryAddScoped<AutomationRuleEngine>();
        if (!services.Any(descriptor =>
            descriptor.ServiceType == typeof(IMigrationSource)
            && descriptor.ImplementationType == typeof(AutomationMigrationSource)))
        {
            services.AddSingleton<IMigrationSource, AutomationMigrationSource>();
        }
        return services;
    }

    /// <summary>Registriert ausschließlich die beiden expliziten Automation-Consumer.</summary>
    public static IServiceCollection AddAutomationConsumers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<EngagementMessageRecordedAutomationConsumer>();
        services.AddScoped<IIntegrationEventHandlerRegistration>(serviceProvider =>
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                EngagementMessageRecordedAutomationConsumer.ConsumerName,
                serviceProvider.GetRequiredService<EngagementMessageRecordedAutomationConsumer>()));
        services.AddScoped<ShopPurchaseCompletedAutomationConsumer>();
        services.AddScoped<IIntegrationEventHandlerRegistration>(serviceProvider =>
            new IntegrationEventHandlerRegistration<ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedAutomationConsumer.ConsumerName,
                serviceProvider.GetRequiredService<ShopPurchaseCompletedAutomationConsumer>()));
        return services;
    }
}
