using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Economy;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Economy-Vertical-Slices.
/// </summary>
public static class EconomyModule
{
    /// <summary>
    /// Registriert die Economy-Use-Cases, den atomaren Store und die Migration.
    /// </summary>
    public static IServiceCollection AddEconomyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommunityEconomyStore, CommunityEconomyStore>();
        services.AddScoped<IEconomyBalanceCredit, EconomyBalanceCredit>();
        services.AddScoped<IEconomyBalanceDebit, EconomyBalanceDebit>();
        services.AddScoped<CreditEconomyBalance>();
        services.AddScoped<DebitEconomyBalance>();
        services.AddSingleton<IMigrationSource, EconomyMigrationSource>();

        return services;
    }
}
