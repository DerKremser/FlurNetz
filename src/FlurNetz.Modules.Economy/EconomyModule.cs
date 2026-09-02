using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Economy.Migrations;
using FlurNetz.Modules.Economy.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.AddEconomyDebitCapability();
        services.AddEconomyCreditCapability();
        services.AddScoped<CreditEconomyBalance>();
        services.AddScoped<DebitEconomyBalance>();

        return services;
    }

    /// <summary>
    /// Registriert ausschließlich die transaction-aware Economy-Debit-Fähigkeit und die
    /// bestehende Economy-Migrationsquelle.
    /// </summary>
    public static IServiceCollection AddEconomyDebitCapability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICommunityEconomyStore, CommunityEconomyStore>();
        services.TryAddScoped<IEconomyBalanceDebit, EconomyBalanceDebit>();
        if (!services.Any(descriptor =>
            descriptor.ServiceType == typeof(IMigrationSource)
            && descriptor.ImplementationType == typeof(EconomyMigrationSource)))
        {
            services.AddSingleton<IMigrationSource, EconomyMigrationSource>();
        }

        return services;
    }

    /// <summary>
    /// Registriert ausschließlich die transaction-aware Economy-Credit-Fähigkeit.
    /// </summary>
    public static IServiceCollection AddEconomyCreditCapability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICommunityEconomyStore, CommunityEconomyStore>();
        services.TryAddScoped<IEconomyBalanceCredit, EconomyBalanceCredit>();
        if (!services.Any(descriptor =>
            descriptor.ServiceType == typeof(IMigrationSource)
            && descriptor.ImplementationType == typeof(EconomyMigrationSource)))
        {
            services.AddSingleton<IMigrationSource, EconomyMigrationSource>();
        }

        return services;
    }
}
