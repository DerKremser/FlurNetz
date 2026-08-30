using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Migrations;
using FlurNetz.Modules.Rewards.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Rewards;

/// <summary>
/// Registriert den ersten persistierten Rewards-Vertical-Slice.
/// </summary>
/// <remarks>
/// Die Registrierung enthält nur Katalog, atomaren Grant-Executor, Use Cases und die eigene
/// Migration. Es gibt weiterhin keine Runtime-, Messaging-, API- oder Worker-Anbindung.
/// </remarks>
public static class RewardsModule
{
    /// <summary>
    /// Registriert Rewards-Katalog, Grant-Ausführung und Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung für weitere Registrierungen.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="services"/> fehlt.</exception>
    public static IServiceCollection AddRewardsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRewardCatalogStore, PostgreSqlRewardCatalogStore>();
        services.AddScoped<CreateEconomyBalanceRewardDefinition>();
        services.AddScoped<CreateRewardPackage>();
        services.AddScoped<IRewardPackageGrantExecutor, PostgreSqlRewardPackageGrantExecutor>();
        services.AddScoped<GrantRewardPackage>();
        services.AddSingleton<IMigrationSource, RewardsMigrationSource>();

        return services;
    }
}
