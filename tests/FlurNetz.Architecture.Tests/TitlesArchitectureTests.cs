using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using FlurNetz.Modules.Titles.Migrations;
using FlurNetz.Modules.Titles.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert Umfang, Ownership und Abhängigkeitsgrenzen des Titles-Vertical-Slices.
/// </summary>
public sealed class TitlesArchitectureTests
{
    private static Assembly TitlesImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Titles");

    private static Assembly TitlesContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Titles.Contracts");

    [Fact]
    public void TitlesImplementationReferencesOnlyApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(TitlesImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Titles.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards", references);
        Assert.DoesNotContain("FlurNetz.Modules.Achievements", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop", references);
        Assert.DoesNotContain("FlurNetz.Modules.Inventory", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression", references);
        Assert.DoesNotContain("FlurNetz.Modules.Economy", references);
        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void TitlesContractsRemainEmpty()
    {
        Assert.Empty(TitlesContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void TitlesDomainTypesRemainInTheImplementationAssembly()
    {
        var expectedTypeNames = new[]
        {
            "FlurNetz.Modules.Titles.Domain.TitleDefinitionId",
            "FlurNetz.Modules.Titles.Domain.TitleNotUnlockedException",
            "FlurNetz.Modules.Titles.Domain.CommunityTitles"
        };

        foreach (var typeName in expectedTypeNames)
        {
            Assert.NotNull(TitlesImplementationAssembly.GetType(typeName));
            Assert.Null(TitlesContractsAssembly.GetType(typeName));
        }
    }

    [Fact]
    public void TitlesApplicationPersistenceMigrationAndModuleTypesRemainInTheImplementationAssembly()
    {
        var expectedTypes = new[]
        {
            typeof(ICommunityTitlesStore),
            typeof(UnlockCommunityTitle),
            typeof(LockCommunityTitle),
            typeof(SetCurrentCommunityTitle),
            typeof(ClearCurrentCommunityTitle),
            typeof(CommunityTitlesStore),
            typeof(TitlesMigrationSource),
            typeof(TitlesModule)
        };

        foreach (var expectedType in expectedTypes)
        {
            Assert.Equal(TitlesImplementationAssembly, expectedType.Assembly);
            Assert.DoesNotContain(expectedType, TitlesContractsAssembly.GetTypes());
        }
    }

    [Fact]
    public void TitlesProductiveTypesUseOnlyTheApprovedSliceNamespaces()
    {
        var invalidTypes = TitlesImplementationAssembly
            .GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(type => !IsAllowedTitlesType(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void TitlesContainsNoPrematureCatalogOrIntegrationTypes()
    {
        Assert.Null(TitlesImplementationAssembly.GetType("FlurNetz.Modules.Titles.Domain.TitleDefinition"));

        var forbiddenNameParts = new[]
        {
            "Event",
            "Message",
            "Inbox",
            "Outbox",
            "Reward",
            "Achievement",
            "Shop",
            "Product",
            "Purchase",
            "Price",
            "Cost",
            "Grant"
        };

        var forbiddenTypes = TitlesImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void CommunityTitlesExposesRehydrateWithinTheDomainAssembly()
    {
        var method = typeof(CommunityTitles).GetMethod(
            nameof(CommunityTitles.Rehydrate),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [
                typeof(CommunityIdentityId),
                typeof(IEnumerable<TitleDefinitionId>),
                typeof(TitleDefinitionId?)
            ],
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(CommunityTitles), method!.ReturnType);
        Assert.Equal(TitlesImplementationAssembly, method.DeclaringType!.Assembly);
        Assert.Equal("FlurNetz.Modules.Titles.Domain", method.DeclaringType.Namespace);
    }

    [Fact]
    public void TitlesMigrationOwnsExactlyItsThreeTablesAndOnlyInternalForeignKeys()
    {
        var migration = Assert.Single(new TitlesMigrationSource().GetMigrations());
        var ownTables = new[]
        {
            "community_titles",
            "community_title_unlocks",
            "community_title_selections"
        };

        Assert.Equal("Titles", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateCommunityTitles", migration.Name);
        Assert.Equal(3, migration.Sql.Split(
            "CREATE TABLE IF NOT EXISTS",
            StringSplitOptions.None).Length - 1);

        foreach (var table in ownTables)
        {
            Assert.Contains(table, migration.Sql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("title_definitions", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reward_", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("achievement", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shop", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inventory", migration.Sql, StringComparison.OrdinalIgnoreCase);

        var referencedTables = Regex.Matches(
                migration.Sql,
                @"REFERENCES\s+([a-z_]+)",
                RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(referencedTables);
        Assert.All(referencedTables, table => Assert.Contains(table, ownTables));
        Assert.Contains("REFERENCES community_titles", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REFERENCES community_title_unlocks", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TitlesModuleRegistersOnlyItsOwnSliceComponents()
    {
        var services = new ServiceCollection();

        var result = services.AddTitlesModule();

        Assert.Same(services, result);
        AssertService(services, typeof(ICommunityTitlesStore), typeof(CommunityTitlesStore), ServiceLifetime.Scoped);
        AssertService(services, typeof(UnlockCommunityTitle), typeof(UnlockCommunityTitle), ServiceLifetime.Scoped);
        AssertService(services, typeof(LockCommunityTitle), typeof(LockCommunityTitle), ServiceLifetime.Scoped);
        AssertService(services, typeof(SetCurrentCommunityTitle), typeof(SetCurrentCommunityTitle), ServiceLifetime.Scoped);
        AssertService(services, typeof(ClearCurrentCommunityTitle), typeof(ClearCurrentCommunityTitle), ServiceLifetime.Scoped);
        AssertService(services, typeof(IMigrationSource), typeof(TitlesMigrationSource), ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.FullName is not null &&
            (descriptor.ServiceType.FullName.Contains("Messaging", StringComparison.Ordinal)
             || descriptor.ServiceType.FullName.Contains("Reward", StringComparison.Ordinal)
             || descriptor.ServiceType.FullName.Contains("Api", StringComparison.Ordinal)));
    }

    [Fact]
    public void TitlesContainsNoGenericRepositoryTypes()
    {
        var forbiddenTypes = TitlesImplementationAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void TitlesContainsNoExternalPlatformIdentityTypes()
    {
        var forbiddenTypes = TitlesImplementationAssembly
            .GetTypes()
            .Where(type => ModuleArchitectureCatalog.ExternalPlatformNames.Any(platformName =>
                type.Name.StartsWith(platformName, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static void AssertService(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services, service => service.ServiceType == serviceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static bool IsAllowedTitlesType(Type type)
    {
        const string titlesNamespace = "FlurNetz.Modules.Titles";
        var allowedNamespacePrefixes = new[]
        {
            titlesNamespace + ".Domain",
            titlesNamespace + ".Application",
            titlesNamespace + ".Persistence",
            titlesNamespace + ".Migrations"
        };

        return string.Equals(type.FullName, titlesNamespace + ".TitlesModule", StringComparison.Ordinal)
            || (type.Namespace is not null && allowedNamespacePrefixes.Any(prefix =>
                string.Equals(type.Namespace, prefix, StringComparison.Ordinal)
                || type.Namespace.StartsWith(prefix + ".", StringComparison.Ordinal)));
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
