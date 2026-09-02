using System.Reflection;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Migrations;
using FlurNetz.Modules.Integrations.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>Sichert die Mapping-, Contract- und Abhängigkeitsgrenzen von Integrations V1.</summary>
public sealed class IntegrationsArchitectureTests
{
    [Fact]
    public void ContractsExposeOnlyResolutionTypesAndIdentityCapability()
    {
        var assembly = typeof(IExternalIdentityResolution).Assembly;
        var exportedTypes = assembly.GetExportedTypes();

        Assert.Equal(
            [
                typeof(ExternalUserId),
                typeof(IExternalIdentityResolution),
                typeof(IntegrationProviderKey)
            ],
            exportedTypes.OrderBy(type => type.FullName, StringComparer.Ordinal));
        Assert.DoesNotContain(exportedTypes, type => type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(exportedTypes, type => type.Name.Contains("Store", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImplementationReferencesNoForeignImplementationOrHost()
    {
        var assembly = typeof(IntegrationsModule).Assembly;
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
            .Select(name => name!)
            .ToArray();

        Assert.All(
            references,
            reference => Assert.Contains(
                reference,
                new[]
                {
                    "FlurNetz.Modules.Integrations.Contracts",
                    "FlurNetz.Modules.Identity.Contracts",
                    "FlurNetz.Persistence"
                }));
        Assert.DoesNotContain(references, reference => reference is "FlurNetz.Api" or "FlurNetz.Worker" or "FlurNetz.Modules.Administration");
    }

    [Fact]
    public void MigrationOwnsOnlyTheMappingTableWithoutCrossModuleForeignKeys()
    {
        var migration = Assert.Single(new IntegrationsMigrationSource().GetMigrations());

        Assert.Equal("Integrations", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateExternalIdentityMappings", migration.Name);
        Assert.Contains("integration_external_identity_mappings", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (provider_key, external_user_id)", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistrationProvidesMappingCapabilityUseCasesAndMigration()
    {
        var services = new ServiceCollection();

        services.AddIntegrationsModule();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IExternalIdentityMappingStore)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IExternalIdentityResolution)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMigrationSource)
                && descriptor.ImplementationType == typeof(IntegrationsMigrationSource)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(LinkExternalIdentity));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ResolveExternalIdentity));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(GetExternalIdentityMapping));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ListExternalIdentityMappings));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(UnlinkExternalIdentity));
    }

    [Fact]
    public void PublicImplementationTypesStayInTheIntegrationsNamespace()
    {
        var invalidTypes = typeof(IntegrationsModule).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace is null
                || !type.Namespace.StartsWith("FlurNetz.Modules.Integrations", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }
}
