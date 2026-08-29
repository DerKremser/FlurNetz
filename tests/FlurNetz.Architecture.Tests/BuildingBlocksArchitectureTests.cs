using System.Reflection;
using FlurNetz.BuildingBlocks.Results;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die bewusst niedrige Abhängigkeits- und Inhaltsgrenze der BuildingBlocks-Schicht.
/// </summary>
public sealed class BuildingBlocksArchitectureTests
{
    // BuildingBlocks ist die unterste gemeinsame technische Schicht.
    // Referenzen auf Persistence, Messaging oder Fachmodule würden die Abhängigkeitsrichtung umkehren.
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

    // Fachliche Typen gehören in Module und dürfen nicht in die wiederverwendbare Basisschicht durchsickern.
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
        // Ein einheitlicher Namespace hält die öffentliche Oberfläche der Basisschicht auffindbar und eindeutig.
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
        // Diese Regel verhindert, dass die technische Schicht unbemerkt fachliche Besitzverhältnisse übernimmt.
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
        // Das Testprojekt darf keine produktiven Exporte vortäuschen, die Architekturregeln umgehen könnten.
        var nonTestTypes = typeof(BuildingBlocksArchitectureTests).Assembly
            .GetExportedTypes()
            .Where(type => !type.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(nonTestTypes);
    }
}
