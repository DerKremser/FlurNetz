using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Inventory.Migrations;
using FlurNetz.Modules.Inventory.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Inventory;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des persistierten Inventory-Slices.
/// </summary>
public static class InventoryModule
{
    /// <summary>
    /// Registriert die Inventory-Use-Cases, den atomaren Store und die Migration.
    /// </summary>
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityInventoryStore, CommunityInventoryStore>();
        services.AddScoped<IInventoryQuantityGrant, InventoryQuantityGrant>();
        services.AddScoped<AddInventoryQuantity>();
        services.AddScoped<RemoveInventoryQuantity>();
        services.AddSingleton<IMigrationSource, InventoryMigrationSource>();

        return services;
    }
}
