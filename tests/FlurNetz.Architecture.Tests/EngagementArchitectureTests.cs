using System.Reflection;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Engagement.Migrations;
using FlurNetz.Modules.Engagement.Persistence;
using FlurNetz.Persistence.Configuration;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den bewusst kleinen fachlichen Umfang und die Abhängigkeitsgrenze von Engagement.
/// </summary>
public sealed class EngagementArchitectureTests
{
    private static Assembly EngagementImplementationAssembly => typeof(EngagementActivity).Assembly;

    private static Assembly EngagementContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Engagement.Contracts");

    [Fact]
    public void EngagementImplementationReferencesRequiredContractsAndTechnicalProjects()
    {
        var references = GetReferencedAssemblyNames(EngagementImplementationAssembly);

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains(typeof(IClock).Assembly.GetName().Name!, references);
        Assert.Contains(typeof(PostgreSqlOptions).Assembly.GetName().Name!, references);
    }

    [Fact]
    public void EngagementImplementationReferencesNoForbiddenProjects()
    {
        var references = GetReferencedAssemblyNames(EngagementImplementationAssembly);

        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);

        var unexpectedModuleReferences = references
            .Where(name => name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal))
            .Where(name => name is not "FlurNetz.Modules.Engagement.Contracts"
                and not "FlurNetz.Modules.Identity.Contracts")
            .ToArray();

        Assert.Empty(unexpectedModuleReferences);
    }

    [Fact]
    public void EngagementContractsRemainEmpty()
    {
        Assert.Empty(EngagementContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void EngagementActivityIdBelongsToTheImplementationAssembly()
    {
        Assert.Equal(
            "FlurNetz.Modules.Engagement",
            typeof(EngagementActivityId).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(EngagementActivityId), EngagementContractsAssembly.GetTypes());
    }

    [Fact]
    public void EngagementSupportsOnlyTheMessageActivityType()
    {
        Assert.Equal(
            [EngagementActivityType.Message],
            Enum.GetValues<EngagementActivityType>());
    }

    [Fact]
    public void RepositoryAndMigrationRemainInTheImplementationAssembly()
    {
        Assert.Equal(EngagementImplementationAssembly, typeof(IEngagementActivityRepository).Assembly);
        Assert.Equal(EngagementImplementationAssembly, typeof(EngagementActivityRepository).Assembly);
        Assert.Equal(EngagementImplementationAssembly, typeof(EngagementMigrationSource).Assembly);
        Assert.DoesNotContain(typeof(IEngagementActivityRepository), EngagementContractsAssembly.GetTypes());
    }

    [Fact]
    public void EngagementMigrationHasNoIdentityForeignKey()
    {
        var migration = Assert.Single(new EngagementMigrationSource().GetMigrations());

        Assert.Equal("Engagement", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateEngagementActivities", migration.Name);
        Assert.DoesNotContain("REFERENCES community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EngagementImplementationContainsNoGenericRepositoryTypes()
    {
        var forbiddenTypes = EngagementImplementationAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void EngagementDomainContainsNoExternalPlatformIdentityTypes()
    {
        var forbiddenTypes = EngagementImplementationAssembly
            .GetTypes()
            .Where(IsExternalPlatformIdentityType)
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToArray();

    private static bool IsExternalPlatformIdentityType(Type type)
    {
        var typeName = type.Name.Split('`')[0];

        return ModuleArchitectureCatalog.ExternalPlatformNames.Any(platformName =>
            typeName.StartsWith(platformName, StringComparison.Ordinal)
            && (typeName.EndsWith("Id", StringComparison.Ordinal)
                || typeName.EndsWith("Identifier", StringComparison.Ordinal)
                || typeName.EndsWith("Identity", StringComparison.Ordinal)));
    }
}
