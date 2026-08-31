using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Achievements;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Achievements.Migrations;
using FlurNetz.Modules.Achievements.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert Umfang, Ownership und Abhängigkeitsgrenzen des Achievements-Slices.
/// </summary>
public sealed class AchievementsArchitectureTests
{
    private static Assembly ImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Achievements");

    private static Assembly ContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Achievements.Contracts");

    [Fact]
    public void ImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(ImplementationAssembly);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Achievements.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.BuildingBlocks",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.BuildingBlocks", references);
        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Engagement.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Inventory.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Titles.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.All(references, reference => Assert.Contains(reference, allowed));
    }

    [Fact]
    public void ContractsRemainEmpty()
    {
        Assert.Empty(ContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void DomainApplicationPersistenceMigrationAndModuleTypesStayInImplementationAssembly()
    {
        var expectedTypes = new[]
        {
            typeof(AchievementDefinitionId),
            typeof(AchievementDefinition),
            typeof(CommunityAchievement),
            typeof(IAchievementDefinitionStore),
            typeof(ICommunityAchievementStore),
            typeof(AchievementDefinitionNotFoundException),
            typeof(CreateAchievementDefinition),
            typeof(GetAchievementDefinition),
            typeof(ListAchievementDefinitions),
            typeof(RenameAchievementDefinition),
            typeof(ChangeAchievementDescription),
            typeof(UnlockCommunityAchievement),
            typeof(GetCommunityAchievement),
            typeof(ListCommunityAchievements),
            typeof(AchievementDefinitionStore),
            typeof(CommunityAchievementStore),
            typeof(AchievementsMigrationSource),
            typeof(AchievementsModule)
        };

        foreach (var expectedType in expectedTypes)
        {
            Assert.Equal(ImplementationAssembly, expectedType.Assembly);
            Assert.DoesNotContain(expectedType, ContractsAssembly.GetTypes());
        }
    }

    [Fact]
    public void ProductiveTypesUseOnlyTheApprovedSliceNamespaces()
    {
        const string root = "FlurNetz.Modules.Achievements";
        var allowedPrefixes = new[]
        {
            root + ".Domain",
            root + ".Application",
            root + ".Persistence",
            root + ".Migrations"
        };

        var invalidTypes = ImplementationAssembly
            .GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(type => !string.Equals(type.FullName, root + ".AchievementsModule", StringComparison.Ordinal))
            .Where(type => type.Namespace is null || !allowedPrefixes.Any(prefix =>
                string.Equals(type.Namespace, prefix, StringComparison.Ordinal)
                || type.Namespace.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void DomainAndApplicationExposeNoPersistenceTypes()
    {
        var domainAndApplicationTypes = ImplementationAssembly
            .GetTypes()
            .Where(type => type.Namespace is not null
                && (type.Namespace.StartsWith("FlurNetz.Modules.Achievements.Domain", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("FlurNetz.Modules.Achievements.Application", StringComparison.Ordinal)))
            .ToArray();
        var memberText = domainAndApplicationTypes
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(member => member.ToString() ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(memberText, value => value.Contains("Dapper", StringComparison.Ordinal));
        Assert.DoesNotContain(memberText, value => value.Contains("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(memberText, value => value.Contains("FlurNetz.Persistence", StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationOwnsExactlyTheTwoAchievementTablesAndOnlyTheDefinitionForeignKey()
    {
        var migration = Assert.Single(new AchievementsMigrationSource().GetMigrations());

        Assert.Equal("Achievements", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateAchievementDefinitionsAndCommunityAchievements", migration.Name);
        Assert.Equal(
            2,
            migration.Sql.Split("CREATE TABLE IF NOT EXISTS", StringSplitOptions.None).Length - 1);
        Assert.Contains("achievement_definitions", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("community_achievements", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (community_identity_id, achievement_definition_id)", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REFERENCES achievement_definitions", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Messaging", migration.Sql, StringComparison.OrdinalIgnoreCase);

        var referencedTables = Regex.Matches(
                migration.Sql,
                @"REFERENCES\s+([a-z_]+)",
                RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(["achievement_definitions"], referencedTables);
    }

    [Fact]
    public void ModuleRegistersOnlyTheSliceAndDoesNotReplaceTheClock()
    {
        var services = new ServiceCollection();

        var result = services.AddAchievementsModule();

        Assert.Same(services, result);
        AssertService(services, typeof(IAchievementDefinitionStore), typeof(AchievementDefinitionStore));
        AssertService(services, typeof(ICommunityAchievementStore), typeof(CommunityAchievementStore));
        AssertService(services, typeof(CreateAchievementDefinition), typeof(CreateAchievementDefinition));
        AssertService(services, typeof(GetAchievementDefinition), typeof(GetAchievementDefinition));
        AssertService(services, typeof(ListAchievementDefinitions), typeof(ListAchievementDefinitions));
        AssertService(services, typeof(RenameAchievementDefinition), typeof(RenameAchievementDefinition));
        AssertService(services, typeof(ChangeAchievementDescription), typeof(ChangeAchievementDescription));
        AssertService(services, typeof(UnlockCommunityAchievement), typeof(UnlockCommunityAchievement));
        AssertService(services, typeof(GetCommunityAchievement), typeof(GetCommunityAchievement));
        AssertService(services, typeof(ListCommunityAchievements), typeof(ListCommunityAchievements));
        AssertService(services, typeof(IMigrationSource), typeof(AchievementsMigrationSource), ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IClock));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName is not null
            && (descriptor.ServiceType.FullName.Contains("Messaging", StringComparison.Ordinal)
                || descriptor.ServiceType.FullName.Contains("Reward", StringComparison.Ordinal)
                || descriptor.ServiceType.FullName.Contains("Api", StringComparison.Ordinal)
                || descriptor.ServiceType.FullName.Contains("Worker", StringComparison.Ordinal)));
    }

    [Fact]
    public void SliceContainsNoPrematureIntegrationOrGenericRepositoryTypes()
    {
        var forbiddenNameParts = new[]
        {
            "Event", "Message", "Inbox", "Outbox", "Reward", "Shop", "Progression",
            "Economy", "Inventory", "Title", "Trigger", "Rule", "Evaluator", "Repository"
        };
        var forbiddenTypes = ImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(part => type.Name.Contains(part, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static void AssertService(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var descriptor = Assert.Single(services, service => service.ServiceType == serviceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
