using System.Data.Common;
using System.Reflection;
using FlurNetz.BuildingBlocks.Results;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using FlurNetz.Modules.Identity.Migrations;

namespace FlurNetz.Architecture.Tests;

public sealed class IdentityArchitectureTests
{
    private static Assembly IdentityContractsAssembly => typeof(CommunityIdentityId).Assembly;
    private static Assembly IdentityImplementationAssembly => typeof(CommunityIdentity).Assembly;

    [Fact]
    public void IdentityContractsReferenceNoImplementationPersistenceOrMessaging()
    {
        var references = GetReferencedAssemblyNames(IdentityContractsAssembly);

        Assert.DoesNotContain("FlurNetz.Modules.Identity", references);
        Assert.DoesNotContain("FlurNetz.Persistence", references);
        Assert.DoesNotContain("FlurNetz.Messaging", references);
    }

    [Fact]
    public void IdentityImplementationReferencesNoForeignModuleImplementation()
    {
        var forbiddenReferences = GetReferencedAssemblyNames(IdentityImplementationAssembly)
            .Where(name => name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal)
                && !StringComparer.Ordinal.Equals(name, "FlurNetz.Modules.Identity.Contracts"))
            .ToArray();

        Assert.Empty(forbiddenReferences);
        Assert.Contains("FlurNetz.Persistence", GetReferencedAssemblyNames(IdentityImplementationAssembly));
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
    public void IdentityContractsExposeOnlyIdentifiersReadsAndTransactionAwareCapabilities()
    {
        var exportedTypes = IdentityContractsAssembly.GetExportedTypes().ToHashSet();

        Assert.True(exportedTypes.SetEquals(
        [
            typeof(CommunityIdentityId),
            typeof(ICommunityIdentityExistence),
            typeof(CommunityIdentitySummary),
            typeof(CommunityIdentityPage),
            typeof(ICommunityIdentityRead),
            typeof(ICommunityIdentityCreator)
        ]));

        var method = typeof(ICommunityIdentityExistence).GetMethod(
            nameof(ICommunityIdentityExistence.ExistsAsync),
            [
                typeof(CommunityIdentityId),
                typeof(DbConnection),
                typeof(DbTransaction),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method!.ReturnType);

        var creatorMethod = typeof(ICommunityIdentityCreator).GetMethod(
            nameof(ICommunityIdentityCreator.CreateAsync),
            [typeof(DbConnection), typeof(DbTransaction), typeof(CancellationToken)]);
        Assert.NotNull(creatorMethod);
        Assert.Equal(typeof(Task<CommunityIdentityId>), creatorMethod!.ReturnType);
    }

    [Fact]
    public void CentralIdentityAssembliesContainNoExternalPlatformIdentityTypes()
    {
        var centralIdentityTypes = IdentityContractsAssembly.GetTypes()
            .Concat(IdentityImplementationAssembly.GetTypes())
            .Where(type =>
            {
                var typeName = type.Name.Split((char)96)[0];
                return ModuleArchitectureCatalog.ExternalPlatformNames.Any(platformName =>
                    typeName.StartsWith(platformName, StringComparison.Ordinal)
                    && (typeName.EndsWith("Id", StringComparison.Ordinal)
                        || typeName.EndsWith("Identifier", StringComparison.Ordinal)
                        || typeName.EndsWith("Identity", StringComparison.Ordinal)));
            })
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(centralIdentityTypes);
    }

    [Fact]
    public void IdentityContractsContainNoRepositoryMigrationCommandOrHandlerTypes()
    {
        var forbiddenTypes = IdentityContractsAssembly.GetExportedTypes()
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal)
                || type.Name.Contains("Store", StringComparison.Ordinal)
                || type.Name.Contains("Migration", StringComparison.Ordinal)
                || type.Name.Contains("Command", StringComparison.Ordinal)
                || type.Name.Contains("Handler", StringComparison.Ordinal))
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
        .Select(reference => reference.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToArray();
}
