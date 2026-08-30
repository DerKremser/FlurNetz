using FlurNetz.Modules.Progression.Application;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Progression.Migrations;
using FlurNetz.Modules.Progression.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Progression;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Progression-Vertical-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Diese Registrierung fügt Use Case, atomaren Store, Consumer und Migrationsquelle hinzu.
/// Der konkrete Consumer wird weiterhin durch eine explizite Messaging-Registrierung
/// komponiert; Assembly Scanning und ein dauerhaft laufender Host sind nicht Bestandteil.
/// </remarks>
public static class ProgressionModule
{
    /// <summary>
    /// Registriert den XP-Use-Case, seinen Persistenzadapter, den Message-Consumer und die Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddProgressionModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityProgressionStore, CommunityProgressionStore>();
        services.AddScoped<GrantExperience>();
        services.AddScoped<MessageEngagementRecordedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandlerRegistration>(serviceProvider =>
            new IntegrationEventHandlerRegistration<MessageEngagementRecordedIntegrationEvent>(
                MessageEngagementRecordedIntegrationEventHandler.ConsumerName,
                serviceProvider.GetRequiredService<MessageEngagementRecordedIntegrationEventHandler>()));
        services.AddSingleton<IMigrationSource, ProgressionMigrationSource>();

        return services;
    }
}
