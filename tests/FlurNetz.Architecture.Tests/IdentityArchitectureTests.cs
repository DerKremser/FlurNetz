using System.Reflection;
using FlurNetz.BuildingBlocks.Results;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Migrations;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die öffentliche Identity-Grenze und die Trennung von externer Plattformidentität.
/// </summary>
public sealed class IdentityArchitectureTests
{
    private static readonly string[] ExternalPlatformNames =
    [
        "Twitch",
        "Discord",
        "YouTube",
        "StreamerBot"
    ];

    private static Assembly IdentityContractsAssembly => typeof(CommunityIdentityId).Assembly;

    private static Assembly IdentityImplementationAssembly => typeof(CommunityIdentity).Assembly;

    [Fact]
    public void IdentityContractsReferenceNoIdentityImplementation()
    {
        var forbiddenReferences = GetReferencedAssemblyNames(IdentityContractsAssembly)
            .Where(name => StringComparer.Ordinal.Equals(name, "FlurNetz.Modules.Identity"))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void IdentityContractsReferenceNoPersistenceOrMessaging()
    {
        var forbiddenReferences = GetReferencedAssemblyNames(IdentityContractsAssembly)
            .Where(name => name is "FlurNetz.Persistence" or "FlurNetz.Messaging")
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void IdentityImplementationReferencesNoForeignModuleImplementation()
    {
        var forbiddenReferences = GetReferencedAssemblyNames(IdentityImplementationAssembly)
            .Where(name => name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal)
                && !StringComparer.Ordinal.Equals(name, "FlurNetz.Modules.Identity.Contracts"))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void IdentityImplementationReferencesPersistenceButNoMessaging()
    {
        var references = GetReferencedAssemblyNames(IdentityImplementationAssembly);

        Assert.Contains("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
    }

    [Fact]
    public void CommunityIdentityIdIsDefinedOnlyInIdentityContracts()
    {
        Assert.Equal("FlurNetz.Modules.Identity.Contracts", IdentityContractsAssembly.GetName().Name);
        Assert.Equal("FlurNetz.Modules.Identity.Contracts", typeof(CommunityIdentityId).Namespace);
        Assert.DoesNotContain(typeof(CommunityIdentityId), IdentityImplementationAssembly.GetExportedTypes());
        Assert.DoesNotContain(typeof(CommunityIdentityId), typeof(Error).Assembly.GetExportedTypes());
    }

    [Fact]
    public void IdentityContractsExposeOnlyTheInternalIdentityIdentifier()
    {
        var exportedTypes = IdentityContractsAssembly.GetExportedTypes();

        Assert.Equal([typeof(CommunityIdentityId)], exportedTypes);
    }

    [Fact]
    public void CentralIdentityAssembliesContainNoExternalPlatformIdentityTypes()
    {
        var centralIdentityTypes = IdentityContractsAssembly.GetTypes()
            .Concat(IdentityImplementationAssembly.GetTypes())
            .Where(IsExternalPlatformIdentityType)
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(centralIdentityTypes);
    }

    [Fact]
    public void IdentityContractsContainNoPersistenceOrApplicationPortTypes()
    {
        var forbiddenTypes = IdentityContractsAssembly.GetExportedTypes()
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal)
                || type.Name.Contains("Store", StringComparison.Ordinal)
                || type.Name.Contains("Migration", StringComparison.Ordinal)
                || type.Name.Contains("Command", StringComparison.Ordinal)
                || type.Name.Contains("Handler", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    [Fact]
    public void IdentityOwnsTheFirstIdentityMigration()
    {
        var migration = Assert.Single(new IdentityMigrationSource().GetMigrations());

        Assert.Equal("Identity", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateCommunityIdentities", migration.Name);
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

        return ExternalPlatformNames.Any(platformName =>
            typeName.StartsWith(platformName, StringComparison.Ordinal)
            && (typeName.EndsWith("Id", StringComparison.Ordinal)
                || typeName.EndsWith("Identifier", StringComparison.Ordinal)
                || typeName.EndsWith("Identity", StringComparison.Ordinal)));
    }
}
