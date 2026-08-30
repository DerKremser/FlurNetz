using System.Reflection;
using FlurNetz.Modules.Progression.Domain;

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
    public void ProgressionImplementationReferencesNoInfrastructureOrMessaging()
    {
        var references = GetReferencedAssemblyNames(ProgressionImplementationAssembly);

        Assert.DoesNotContain("FlurNetz.Persistence", references);
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
    public void FoundationContainsNoLevelEventsPersistenceOrRewards()
    {
        var forbiddenNames = new[]
        {
            "Level",
            "Event",
            "Repository",
            "Migration",
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
