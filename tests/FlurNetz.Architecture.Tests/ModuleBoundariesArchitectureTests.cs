using System.Reflection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die physischen Modul-, Contracts- und Abhängigkeitsgrenzen ab.
/// </summary>
public sealed class ModuleBoundariesArchitectureTests
{
    private static readonly string[] ForbiddenInfrastructurePrefixes =
    [
        "FlurNetz.Persistence",
        "FlurNetz.Api",
        "FlurNetz.Worker"
    ];

    [Fact]
    public void CatalogContainsExactlyThePlannedModules()
    {
        Assert.Equal(14, ModuleArchitectureCatalog.Modules.Count);
        Assert.Equal(
            ModuleArchitectureCatalog.Modules.Count,
            ModuleArchitectureCatalog.Modules.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryKnownModuleAssemblyCanBeLoadedExplicitly()
    {
        foreach (var module in ModuleArchitectureCatalog.Modules)
        {
            var implementation = ModuleArchitectureCatalog.LoadAssembly(module.ImplementationAssemblyName);
            var contracts = ModuleArchitectureCatalog.LoadAssembly(module.ContractsAssemblyName);

            Assert.Equal(module.ImplementationAssemblyName, implementation.GetName().Name);
            Assert.Equal(module.ContractsAssemblyName, contracts.GetName().Name);
        }
    }

    [Fact]
    public void ArchitectureTestReferencesNoUnexpectedModuleAssembly()
    {
        var expectedAssemblyNames = ModuleArchitectureCatalog.Modules
            .SelectMany(module => new[] { module.ImplementationAssemblyName, module.ContractsAssemblyName })
            .ToHashSet(StringComparer.Ordinal);
        var referencedModuleNames = typeof(ModuleBoundariesArchitectureTests).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal))
            .ToArray();

        var unexpectedModuleNames = referencedModuleNames
            .Where(name => !expectedAssemblyNames.Contains(name!))
            .ToArray();

        Assert.Empty(unexpectedModuleNames);
    }

    [Fact]
    public void ImplementationsReferenceOnlyTheirOwnContracts()
    {
        foreach (var module in ModuleArchitectureCatalog.Modules)
        {
            var moduleReferences = ModuleArchitectureCatalog.LoadAssembly(module.ImplementationAssemblyName)
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .Where(name => name is not null && name.StartsWith("FlurNetz.Modules.", StringComparison.Ordinal))
                .ToArray();

            var unexpectedReferences = moduleReferences
                .Where(name => !StringComparer.Ordinal.Equals(name, module.ContractsAssemblyName))
                .ToArray();

            Assert.Empty(unexpectedReferences);
        }
    }

    [Fact]
    public void ContractsReferenceNoImplementationsOrHosts()
    {
        var implementationAssemblyNames = ModuleArchitectureCatalog.Modules
            .Select(module => module.ImplementationAssemblyName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var module in ModuleArchitectureCatalog.Modules)
        {
            var forbiddenReferences = ModuleArchitectureCatalog.LoadAssembly(module.ContractsAssemblyName)
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .Where(name => name is not null &&
                    (implementationAssemblyNames.Contains(name) ||
                     ForbiddenInfrastructurePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))))
                .ToArray();

            Assert.Empty(forbiddenReferences);
        }
    }

    [Fact]
    public void PublicModuleTypesUseTheOwningModuleNamespace()
    {
        foreach (var module in ModuleArchitectureCatalog.Modules)
        {
            var invalidImplementationTypes = ModuleArchitectureCatalog.LoadAssembly(module.ImplementationAssemblyName)
                .GetExportedTypes()
                .Where(type => !IsInNamespace(type, module.ImplementationNamespace))
                .Select(type => type.FullName)
                .ToArray();
            var invalidContractsTypes = ModuleArchitectureCatalog.LoadAssembly(module.ContractsAssemblyName)
                .GetExportedTypes()
                .Where(type => !IsInNamespace(type, module.ContractsNamespace))
                .Select(type => type.FullName)
                .ToArray();

            Assert.Empty(invalidImplementationTypes);
            Assert.Empty(invalidContractsTypes);
        }
    }

    private static bool IsInNamespace(Type type, string expectedNamespace)
    {
        return type.Namespace is not null &&
            (StringComparer.Ordinal.Equals(type.Namespace, expectedNamespace) ||
             type.Namespace.StartsWith(expectedNamespace + ".", StringComparison.Ordinal));
    }
}
