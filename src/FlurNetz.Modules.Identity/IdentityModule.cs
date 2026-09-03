using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Identity;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Identity-Vertical-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Diese Registrierung fügt nur Identity-Use-Case, Repository und Migrationsquelle hinzu.
/// </remarks>
public static class IdentityModule
{
    /// <summary>
    /// Registriert den Identity-Use-Case, seinen Persistenzadapter und die Migrationen.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<CommunityIdentityRepository>();
        services.AddScoped<ICommunityIdentityRepository>(provider =>
            provider.GetRequiredService<CommunityIdentityRepository>());
        services.AddScoped<ICommunityIdentityRead>(provider =>
            provider.GetRequiredService<CommunityIdentityRepository>());
        services.AddScoped<ICommunityIdentityExistence, CommunityIdentityExistence>();
        services.AddScoped<CreateCommunityIdentity>();
        services.AddScoped<ICommunityIdentityCreator>(provider =>
            provider.GetRequiredService<CreateCommunityIdentity>());
        services.AddSingleton<IMigrationSource, IdentityMigrationSource>();

        return services;
    }
}
