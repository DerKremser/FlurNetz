using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Inventory;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des persistierten Inventory-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Diese Registrierung fügt ausschließlich Store, interne Use Cases und die eigene Migration
/// hinzu. Es gibt noch keine Runtime-, Messaging-, Rewards-, Shop- oder API-Komposition.
/// </remarks>
public static class InventoryModule
{
    /// <summary>
    /// Registriert die Inventory-Use-Cases, den atomaren Store und die Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityInventoryStore, CommunityInventoryStore>();
        services.AddScoped<AddInventoryQuantity>();
        services.AddScoped<RemoveInventoryQuantity>();
        services.AddSingleton<IMigrationSource, InventoryMigrationSource>();

        return services;
    }
}
