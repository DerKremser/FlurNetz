using System.Reflection;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Progression.Domain;
using FlurNetz.Modules.Progression.Migrations;
using FlurNetz.Modules.Progression.Persistence;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den bewusst kleinen Umfang und die Abhängigkeitsgrenze der Progression-Foundation.
/// </summary>
public sealed class ProgressionArchitectureTests
{
    private static Assembly ProgressionImplementationAssembly => typeof(CommunityProgression).Assembly;

    private static Assembly ProgressionContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Progression.Contracts");

    [Fact]
    public void ProgressionImplementationReferencesIdentityContractsOnlyAsForeignModule()
    {
        var references = GetReferencedAssemblyNames(ProgressionImplementationAssembly);

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Modules.Engagement", references);
        Assert.DoesNotContain("FlurNetz.Modules.Engagement.Contracts", references);
    }

    [Fact]
    public void ProgressionImplementationReferencesPersistenceButNoMessaging()
    {
        var references = GetReferencedAssemblyNames(ProgressionImplementationAssembly);

        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
    }

    [Fact]
    public void ProgressionContractsRemainEmpty()
    {
        Assert.Empty(ProgressionContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void DomainTypesRemainInTheImplementationAssembly()
    {
        Assert.Equal("FlurNetz.Modules.Progression", ProgressionImplementationAssembly.GetName().Name);
        Assert.DoesNotContain(typeof(ExperiencePoints), ProgressionContractsAssembly.GetTypes());
        Assert.DoesNotContain(typeof(CommunityProgression), ProgressionContractsAssembly.GetTypes());
    }

    [Fact]
    public void SliceContainsNoLevelEventsOrRewards()
    {
        var forbiddenNames = new[]
        {
            "Level",
            "Event",
            "Reward",
            "Coin"
        };

        var forbiddenTypes = ProgressionImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNames.Any(name => type.Name.Contains(name, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void ProgressionPersistenceTypesRemainInTheImplementationAssembly()
    {
        Assert.Equal(ProgressionImplementationAssembly, typeof(ICommunityProgressionStore).Assembly);
        Assert.Equal(ProgressionImplementationAssembly, typeof(CommunityProgressionStore).Assembly);
        Assert.Equal(ProgressionImplementationAssembly, typeof(ProgressionMigrationSource).Assembly);
        Assert.DoesNotContain(typeof(ICommunityProgressionStore), ProgressionContractsAssembly.GetTypes());
    }

    [Fact]
    public void GrantExperienceAndStoreRemainInTheImplementationAssembly()
    {
        Assert.Equal(ProgressionImplementationAssembly, typeof(GrantExperience).Assembly);
        Assert.DoesNotContain(typeof(GrantExperience), ProgressionContractsAssembly.GetTypes());
    }

    [Fact]
    public void ProgressionMigrationHasNoCrossModuleSqlDependency()
    {
        var migration = Assert.Single(new ProgressionMigrationSource().GetMigrations());

        Assert.Equal("Progression", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateCommunityProgressions", migration.Name);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("engagement_activities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REFERENCES", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProgressionContainsNoGenericRepositoryTypes()
    {
        var forbiddenTypes = ProgressionImplementationAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void FoundationContainsNoExternalPlatformIdentityTypes()
    {
        var externalPlatformNames = new[] { "Twitch", "StreamerBot", "Discord", "YouTube", "Kick" };

        var forbiddenTypes = ProgressionImplementationAssembly
            .GetTypes()
            .Where(type => externalPlatformNames.Any(platformName =>
                type.Name.StartsWith(platformName, StringComparison.Ordinal)))
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
}
