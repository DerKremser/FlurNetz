using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Migrations;
using FlurNetz.Modules.Titles.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Titles;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Titles-Vertical-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Titles registriert nur den eigenen Store, die vier internen Use Cases und seine Migration;
/// Host-, API-, Messaging- und Cross-Module-Komposition sind nicht enthalten.
/// </remarks>
public static class TitlesModule
{
    /// <summary>
    /// Registriert Store, Use Cases und Titles-Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddTitlesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityTitlesStore, CommunityTitlesStore>();
        services.AddScoped<UnlockCommunityTitle>();
        services.AddScoped<LockCommunityTitle>();
        services.AddScoped<SetCurrentCommunityTitle>();
        services.AddScoped<ClearCurrentCommunityTitle>();
        services.AddSingleton<IMigrationSource, TitlesMigrationSource>();

        return services;
    }
}
