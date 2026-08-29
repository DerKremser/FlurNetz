using System.Reflection;
using FlurNetz.BuildingBlocks.Results;

namespace FlurNetz.Architecture.Tests;

public sealed class BuildingBlocksArchitectureTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "FlurNetz.Engagement",
        "FlurNetz.Progression",
        "FlurNetz.Economy",
        "FlurNetz.Rewards",
        "FlurNetz.Inventory",
        "FlurNetz.Identity",
        "FlurNetz.Persistence",
        "FlurNetz.Messaging",
        "FlurNetz.Api",
        "FlurNetz.Worker"
    ];

    private static readonly string[] ForbiddenTypeNames =
    [
        "CommunityUser",
        "Wallet",
        "Achievement",
        "InventoryItem",
        "ShopItem"
    ];

    private static Assembly BuildingBlocksAssembly => typeof(Error).Assembly;

    [Fact]
    public void BuildingBlocks_HasNoForbiddenProjectReferences()
    {
        var forbiddenReferences = BuildingBlocksAssembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void BuildingBlocks_UsesConsistentNamespaces()
    {
        var invalidTypes = BuildingBlocksAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace is null || !type.Namespace.StartsWith("FlurNetz.BuildingBlocks.", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void BuildingBlocks_ContainsNoForbiddenDomainTypeNames()
    {
        var forbiddenTypes = BuildingBlocksAssembly
            .GetExportedTypes()
            .Where(type => ForbiddenTypeNames.Contains(type.Name, StringComparer.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void ArchitectureTestAssembly_ContainsOnlyTestTypes()
    {
        var nonTestTypes = typeof(BuildingBlocksArchitectureTests).Assembly
            .GetExportedTypes()
            .Where(type => !type.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(nonTestTypes);
    }
}
