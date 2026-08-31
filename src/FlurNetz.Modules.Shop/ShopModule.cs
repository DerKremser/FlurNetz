using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Shop;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Shop-Katalog-Slices.
/// </summary>
/// <remarks>
/// Die technische PostgreSQL-Verbindungsfabrik bleibt Verantwortung des Composition Roots.
/// Shop registriert ausschließlich seinen Store, die internen Katalog-Use-Cases und die
/// eigene Migration; Host-, API-, Messaging- und Purchase-Komposition sind nicht enthalten.
/// </remarks>
public static class ShopModule
{
    /// <summary>
    /// Registriert den Shop-Store, die Katalog-Use-Cases und die Shop-Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddShopModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IShopOfferStore, ShopOfferStore>();
        services.AddScoped<CreateShopOffer>();
        services.AddScoped<GetShopOffer>();
        services.AddScoped<ListShopOffers>();
        services.AddScoped<RenameShopOffer>();
        services.AddScoped<ChangeShopOfferDescription>();
        services.AddScoped<ChangeShopOfferPrice>();
        services.AddScoped<ChangeShopOfferAvailability>();
        services.AddScoped<ChangeShopOfferPurchaseLimit>();
        services.AddScoped<EnableShopOffer>();
        services.AddScoped<DisableShopOffer>();
        services.AddSingleton<IMigrationSource, ShopMigrationSource>();

        return services;
    }
}
