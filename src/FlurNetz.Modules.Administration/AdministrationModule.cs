using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Migrations;
using FlurNetz.Modules.Administration.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Administration;

/// <summary>Composition-Root-Registrierungen für Administration V1.</summary>
public static class AdministrationModule
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IAdminCredentialStore, AdminCredentialStore>();
        services.AddScoped<IAdminAuditStore, AdminAuditStore>();
        services.AddScoped<IAdminOperationStore, AdminOperationStore>();
        services.AddScoped<IAdminPasswordHasher, AdminPasswordHasher>();
        services.AddScoped<IAdminAuthenticationService, AdminAuthenticationService>();
        services.AddScoped<IAdminFirstRunSetup, AdminFirstRunSetup>();
        services.AddScoped<IAdminCredentialRecovery, AdminCredentialRecovery>();
        services.AddScoped<AdminPasswordChange>();
        services.AddScoped<AdminMutationCoordinator>();
        services.AddScoped<IAdminExecutionContextAccessor, AdminExecutionContextAccessor>();
        services.AddSingleton<IMigrationSource, AdministrationMigrationSource>();
        return services;
    }
}

public sealed class AdminExecutionContextAccessor : IAdminExecutionContextAccessor
{
    public AdminExecutionContext? Current { get; set; }
}
