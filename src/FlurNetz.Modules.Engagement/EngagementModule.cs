using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Migrations;
using FlurNetz.Modules.Engagement.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Engagement;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Engagement-Recording-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Diese Registrierung fügt nur Use Case, Repository, Zeitquelle und Migrationsquelle hinzu.
/// </remarks>
public static class EngagementModule
{
    /// <summary>
    /// Registriert den Message-Recording-Use-Case, seinen Persistenzadapter und die Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddEngagementModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IEngagementActivityRepository, EngagementActivityRepository>();
        services.AddScoped<RecordMessageEngagement>();
        services.AddSingleton<IMigrationSource, EngagementMigrationSource>();

        return services;
    }
}
