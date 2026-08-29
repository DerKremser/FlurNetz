using System.Reflection;
using FlurNetz.Persistence.Configuration;

namespace FlurNetz.Architecture.Tests;

public sealed class PersistenceArchitectureTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "FlurNetz.Engagement",
        "FlurNetz.Progression",
        "FlurNetz.Economy",
        "FlurNetz.Rewards",
        "FlurNetz.Inventory",
        "FlurNetz.Identity",
        "FlurNetz.Messaging",
        "FlurNetz.Api",
        "FlurNetz.Worker",
        "FlurNetz.Integrations"
    ];

    private static Assembly PersistenceAssembly => typeof(PostgreSqlOptions).Assembly;

    [Fact]
    public void PersistenceHasNoForbiddenProjectReferences()
    {
        var forbiddenReferences = PersistenceAssembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null
                && ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void PersistenceUsesConsistentNamespaces()
    {
        var invalidTypes = PersistenceAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace is null
                || !type.Namespace.StartsWith("FlurNetz.Persistence.", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void PersistenceContainsNoGenericRepositoryTypes()
    {
        var forbiddenTypes = PersistenceAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository" or "CrudRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }
}
