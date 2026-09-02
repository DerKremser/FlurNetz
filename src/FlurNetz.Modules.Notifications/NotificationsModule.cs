using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Contracts;
using FlurNetz.Modules.Notifications.Migrations;
using FlurNetz.Modules.Notifications.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlurNetz.Modules.Notifications;

/// <summary>
/// Registriert die persönlichen Notifications-Komponenten und ihre explizite Consumer-Policy.
/// </summary>
public static class NotificationsModule
{
    /// <summary>
    /// Registriert den HTTP-/Application-/Persistence-Scope des Moduls.
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNotificationCreateCapability();
        services.AddScoped<GetNotification>();
        services.AddScoped<ListNotificationsForIdentity>();
        services.AddScoped<GetUnreadNotificationCount>();
        services.AddScoped<MarkNotificationRead>();
        services.AddScoped<MarkNotificationUnread>();
        services.AddScoped<MarkAllNotificationsRead>();
        services.AddSingleton<IMigrationSource, NotificationsMigrationSource>();

        return services;
    }

    /// <summary>
    /// Registriert ausschließlich die schmale transaction-aware Create-Capability und den
    /// vorhandenen Notification-Kern, auf den sie delegiert.
    /// </summary>
    public static IServiceCollection AddNotificationCreateCapability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICommunityNotificationStore, CommunityNotificationStore>();
        services.TryAddScoped<CreateNotification>();
        services.TryAddScoped<ICommunityNotificationCreate, CommunityNotificationCreateCapability>();
        return services;
    }

    /// <summary>
    /// Ergänzt den Worker um den konkreten Shop-Purchase-Consumer.
    /// </summary>
    public static IServiceCollection AddNotificationsConsumer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ShopPurchaseCompletedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandlerRegistration>(serviceProvider =>
            new IntegrationEventHandlerRegistration<FlurNetz.Modules.Shop.Contracts.ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedIntegrationEventHandler.ConsumerName,
                serviceProvider.GetRequiredService<ShopPurchaseCompletedIntegrationEventHandler>()));

        return services;
    }
}
