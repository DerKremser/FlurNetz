using System.Reflection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den bewusst kleinen Umfang und die Abhängigkeitsgrenze der Titles-Foundation.
/// </summary>
public sealed class TitlesArchitectureTests
{
    private static Assembly TitlesImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Titles");

    private static Assembly TitlesContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Titles.Contracts");

    [Fact]
    public void TitlesImplementationReferencesOnlyItsContractsAndIdentityContracts()
    {
        var references = GetReferencedAssemblyNames(TitlesImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Titles.Contracts",
            "FlurNetz.Modules.Identity.Contracts"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Rewards", references);
        Assert.DoesNotContain("FlurNetz.Modules.Achievements", references);
        Assert.DoesNotContain("FlurNetz.Modules.Shop", references);
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
    public void TitlesFoundationContainsProductiveTypesOnlyInDomainNamespace()
    {
        const string domainNamespace = "FlurNetz.Modules.Titles.Domain";

        var typesOutsideDomain = TitlesImplementationAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace is null ||
                (!StringComparer.Ordinal.Equals(type.Namespace, domainNamespace) &&
                 !type.Namespace.StartsWith(domainNamespace + ".", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(typesOutsideDomain);
    }

    [Fact]
    public void TitlesFoundationContainsNoPrematureCatalogOrPersistenceTypes()
    {
        Assert.Null(TitlesImplementationAssembly.GetType("FlurNetz.Modules.Titles.Domain.TitleDefinition"));

        var forbiddenNameParts = new[]
        {
            "Repository",
            "Store",
            "Migration",
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
            "Service",
            "Handler",
            "UseCase",
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
    public void TitlesFoundationHasNoPublicRehydratePath()
    {
        var communityTitles = TitlesImplementationAssembly.GetType(
            "FlurNetz.Modules.Titles.Domain.CommunityTitles");

        Assert.NotNull(communityTitles);

        var methods = communityTitles!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Rehydrate")
            .ToArray();

        Assert.Empty(methods);
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

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
