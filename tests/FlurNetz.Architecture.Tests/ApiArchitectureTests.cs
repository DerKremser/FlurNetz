using System.Reflection;
using FlurNetz.Api;
using FlurNetz.BuildingBlocks.Results;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Configuration;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die API als Composition Root und reine HTTP-Adaptergrenze ab.
/// </summary>
public sealed class ApiArchitectureTests
{
    private static readonly string[] AllowedApiProjectReferences =
    [
        "FlurNetz.Messaging",
        "FlurNetz.Modules.Economy",
        "FlurNetz.Persistence",
        "FlurNetz.Modules.Identity",
        "FlurNetz.Modules.Identity.Contracts",
        "FlurNetz.Modules.Inventory",
        "FlurNetz.Modules.Shop",
        "FlurNetz.Modules.Shop.Contracts"
    ];

    private static Assembly ApiAssembly => typeof(Program).Assembly;

    [Fact]
    public void ApiReferencesTheRequiredCompositionRootProjects()
    {
        var references = GetReferencedAssemblyNames(ApiAssembly);

        Assert.Contains("FlurNetz.Messaging", references);
        Assert.Contains("FlurNetz.Modules.Economy", references);
        Assert.Contains("FlurNetz.Persistence", references);
        Assert.Contains("FlurNetz.Modules.Identity", references);
        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Inventory", references);
        Assert.Contains("FlurNetz.Modules.Shop", references);
        Assert.Contains("FlurNetz.Modules.Shop.Contracts", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
    }

    [Fact]
    public void ApiReferencesNoUnapprovedFlurNetzProject()
    {
        var unexpectedReferences = GetReferencedAssemblyNames(ApiAssembly)
            .Where(name => name.StartsWith("FlurNetz.", StringComparison.Ordinal))
            .Where(name => !AllowedApiProjectReferences.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.Empty(unexpectedReferences);
    }

    [Fact]
    public void ModulesAndTechnicalLayersDoNotReferenceApi()
    {
        var assemblies = ModuleArchitectureCatalog.Modules
            .SelectMany(module => new[]
            {
                ModuleArchitectureCatalog.LoadAssembly(module.ImplementationAssemblyName),
                ModuleArchitectureCatalog.LoadAssembly(module.ContractsAssemblyName)
            })
            .Append(typeof(Error).Assembly)
            .Append(typeof(PostgreSqlOptions).Assembly)
            .Append(typeof(IntegrationEventEnvelope).Assembly);

        var invalidReferences = assemblies
            .SelectMany(assembly => GetReferencedAssemblyNames(assembly)
                .Where(name => StringComparer.Ordinal.Equals(name, "FlurNetz.Api"))
                .Select(name => $"{assembly.GetName().Name} -> {name}"))
            .ToArray();

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void PublicApiTypesUseTheApiNamespace()
    {
        var invalidTypes = ApiAssembly.GetTypes()
            .Where(type => type.Namespace is null
                || !type.Namespace.StartsWith("FlurNetz.Api", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void ApiContainsNoRepositoryDomainOrMigrationTypes()
    {
        var invalidTypes = ApiAssembly.GetExportedTypes()
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal)
                || type.Name.Contains("Migration", StringComparison.Ordinal)
                || type.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToArray();
}
