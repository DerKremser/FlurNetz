using System.Reflection;
using FlurNetz.Modules.Engagement.Domain;

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
    public void EngagementImplementationReferencesIdentityContractsButNotIdentityImplementation()
    {
        var references = GetReferencedAssemblyNames(EngagementImplementationAssembly);

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
    }

    [Fact]
    public void EngagementImplementationReferencesNoPersistenceMessagingOrProgression()
    {
        var references = GetReferencedAssemblyNames(EngagementImplementationAssembly);

        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression", references);
        Assert.DoesNotContain("FlurNetz.Modules.Progression.Contracts", references);
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
