using System.Reflection;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Administration.Migrations;

namespace FlurNetz.Architecture.Tests;

public sealed class AdministrationArchitectureTests
{
    private static Assembly ContractsAssembly => typeof(PermissionCatalog).Assembly;
    private static Assembly ImplementationAssembly => typeof(AdminCredential).Assembly;

    [Fact]
    public void AdministrationContractsDoNotReferenceImplementationApiPersistenceOrWorker()
    {
        var references = GetReferencedAssemblyNames(ContractsAssembly);

        Assert.DoesNotContain("FlurNetz.Modules.Administration", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
    }

    [Fact]
    public void AdministrationImplementationUsesOnlyItsContractsIdentityContractsAndPersistence()
    {
        var references = GetReferencedAssemblyNames(ImplementationAssembly);
        var forbiddenModuleImplementations = references
            .Where(name => name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal)
                && name is not "FlurNetz.Modules.Administration.Contracts"
                && name is not "FlurNetz.Modules.Identity.Contracts")
            .ToArray();

        Assert.Empty(forbiddenModuleImplementations);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.Contains("FlurNetz.Persistence", references);
    }

    [Fact]
    public void AdministrationMigrationContainsOnlyAdministrationOwnedTables()
    {
        var sql = Assert.Single(new AdministrationMigrationSource().GetMigrations()).Sql;

        Assert.Contains("administration_credentials", sql, StringComparison.Ordinal);
        Assert.Contains("administration_role_assignments", sql, StringComparison.Ordinal);
        Assert.Contains("administration_audit_entries", sql, StringComparison.Ordinal);
        Assert.Contains("administration_operations", sql, StringComparison.Ordinal);
        Assert.Contains("administration_setup_state", sql, StringComparison.Ordinal);

        foreach (var foreignTable in new[]
        {
            "community_identities", "economy_balances", "progression", "inventory",
            "titles", "achievements", "rewards", "shop_offers", "notifications",
            "automation_rules", "external_identity_mappings", "overlay_channels"
        })
        {
            Assert.DoesNotContain(foreignTable, sql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("REFERENCES", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToArray();
}
