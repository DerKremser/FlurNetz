using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlurNetz.Modules.Shop;

/// <summary>
/// Registriert den persistierten Shop-Katalog, die read-only Kaufhistorie und den atomaren
/// Inventory-Purchase-Slice.
/// </summary>
/// <remarks>
/// Connection Factory, Messaging-Serializer, Event-Registry und Outbox-Publisher bleiben
/// Verantwortung des Composition Roots beziehungsweise ihrer technischen Module.
/// </remarks>
public static class ShopModule
{
    /// <summary>
    /// Registriert Shop-Katalog, Purchase-Use-Cases, Kaufhistorie, atomaren Executor und
    /// Shop-Migrationen.
    /// </summary>
    public static IServiceCollection AddShopModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddShopReadOnlyModule();
        services.AddScoped<IShopPurchaseExecutor, PostgreSqlShopPurchaseExecutor>();
        services.AddScoped<CreateShopOffer>();
        services.AddScoped<RenameShopOffer>();
        services.AddScoped<ChangeShopOfferDescription>();
        services.AddScoped<ChangeShopOfferPrice>();
        services.AddScoped<ChangeShopOfferAvailability>();
        services.AddScoped<ChangeShopOfferPurchaseLimit>();
        services.AddScoped<EnableShopOffer>();
        services.AddScoped<DisableShopOffer>();
        services.AddScoped<PurchaseShopOffer>();

        return services;
    }

    /// <summary>
    /// Registriert ausschließlich die read-only Shop-Komponenten für eine Storefront.
    /// </summary>
    /// <remarks>
    /// Der Registration fehlt bewusst jede Purchase-Executor- und Katalogmutations-
    /// Komponente. Dadurch kann ein Host den Shop lesen, ohne Economy, Inventory oder
    /// Messaging für einen nicht angebotenen HTTP-Purchase-Pfad verdrahten zu müssen.
    /// </remarks>
    public static IServiceCollection AddShopReadOnlyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IShopOfferStore, ShopOfferStore>();
        services.AddScoped<IShopPurchaseHistoryStore, ShopPurchaseHistoryStore>();
        services.AddScoped<GetShopOffer>();
        services.AddScoped<ListShopOffers>();
        services.AddScoped<GetAvailableShopOffer>();
        services.AddScoped<ListAvailableShopOffers>();
        services.AddScoped<GetShopPurchase>();
        services.AddScoped<ListShopPurchasesForIdentity>();
        services.AddSingleton<IMigrationSource, ShopMigrationSource>();

        return services;
    }
}
