using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Economy;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Economy-Vertical-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Diese Registrierung fügt ausschließlich Store, interne Use Cases und die eigene Migration
/// hinzu. Es gibt noch keine Runtime-, Messaging-, API- oder Plattform-Komposition.
/// </remarks>
public static class EconomyModule
{
    /// <summary>
    /// Registriert die Economy-Use-Cases, den atomaren Store und die Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddEconomyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityEconomyStore, CommunityEconomyStore>();
        services.AddScoped<CreditEconomyBalance>();
        services.AddScoped<DebitEconomyBalance>();
        services.AddSingleton<IMigrationSource, EconomyMigrationSource>();

        return services;
    }
}
