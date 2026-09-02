using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Migrations;
using FlurNetz.Modules.Integrations.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Integrations;

/// <summary>Registriert den External-Identity-Mapping-Slice.</summary>
public static class IntegrationsModule
{
    /// <summary>
    /// Registriert Store, Use Cases, Resolution-Capability und die Integrationsmigration.
    /// </summary>
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<PostgreSqlExternalIdentityMappingStore>();
        services.AddScoped<IExternalIdentityMappingStore>(provider =>
            provider.GetRequiredService<PostgreSqlExternalIdentityMappingStore>());
        services.AddScoped<IExternalIdentityResolution>(provider =>
            provider.GetRequiredService<PostgreSqlExternalIdentityMappingStore>());
        services.AddScoped<LinkExternalIdentity>();
        services.AddScoped<ResolveExternalIdentity>();
        services.AddScoped<GetExternalIdentityMapping>();
        services.AddScoped<ListExternalIdentityMappings>();
        services.AddScoped<UnlinkExternalIdentity>();
        services.AddSingleton<IMigrationSource, IntegrationsMigrationSource>();

        return services;
    }
}
