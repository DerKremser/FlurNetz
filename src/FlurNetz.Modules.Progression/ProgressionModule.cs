using FlurNetz.Modules.Progression.Application;
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
/// Diese Registrierung fügt nur Use Case, atomaren Store und Migrationsquelle hinzu.
/// Messaging und ein Host-Wiring sind nicht Bestandteil dieses Slices.
/// </remarks>
public static class ProgressionModule
{
    /// <summary>
    /// Registriert den XP-Use-Case, seinen Persistenzadapter und die Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddProgressionModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityProgressionStore, CommunityProgressionStore>();
        services.AddScoped<GrantExperience>();
        services.AddSingleton<IMigrationSource, ProgressionMigrationSource>();

        return services;
    }
}
