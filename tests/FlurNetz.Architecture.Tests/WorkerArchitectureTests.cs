using System.Reflection;
using FlurNetz.Messaging.Integration;
using FlurNetz.Worker;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den Worker als eigenständige, schmale Composition Root ab.
/// </summary>
public sealed class WorkerArchitectureTests
{
    private static readonly string[] AllowedWorkerProjectReferences =
    [
        "FlurNetz.Messaging",
        "FlurNetz.Persistence",
        "FlurNetz.Modules.Progression",
        "FlurNetz.Modules.Notifications",
        "FlurNetz.Modules.Automation",
        "FlurNetz.Modules.Economy",
        "FlurNetz.Modules.Engagement.Contracts",
        "FlurNetz.Modules.Shop.Contracts",
        "FlurNetz.Modules.Overlay"
    ];

    private static readonly string[] ForbiddenWorkerReferences =
    [
        "FlurNetz.Api",
        "FlurNetz.Modules.Engagement",
        "FlurNetz.Modules.Identity",
        "FlurNetz.Modules.Rewards",
        "FlurNetz.Modules.Inventory",
        "FlurNetz.Modules.Titles",
        "FlurNetz.Modules.Achievements",
        "FlurNetz.Modules.Shop",
        "FlurNetz.Modules.Integrations",
        "FlurNetz.Modules.Administration"
    ];

    private static Assembly WorkerAssembly => typeof(Program).Assembly;

    [Fact]
    public void WorkerReferencesOnlyItsRuntimeCompositionProjects()
    {
        var references = GetReferencedAssemblyNames(WorkerAssembly);

        foreach (var requiredReference in AllowedWorkerProjectReferences)
        {
            Assert.Contains(requiredReference, references);
        }

        var forbiddenReferences = references
            .Where(reference => ForbiddenWorkerReferences.Contains(reference, StringComparer.Ordinal))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void WorkerDoesNotReferenceApiOrHostItself()
    {
        var references = GetReferencedAssemblyNames(WorkerAssembly);

        Assert.DoesNotContain("FlurNetz.Api", references);
        Assert.DoesNotContain("FlurNetz.Worker", references);
    }

    [Fact]
    public void WorkerDefinesNoIntegrationEventsOrDomainTypes()
    {
        var invalidTypes = WorkerAssembly
            .GetTypes()
            .Where(type => (!type.IsInterface && typeof(IIntegrationEvent).IsAssignableFrom(type))
                || type.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true
                || type.Name.Contains("Repository", StringComparison.Ordinal)
                || type.Name.Contains("Aggregate", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void WorkerTypesStayInTheWorkerNamespace()
    {
        var invalidTypes = WorkerAssembly
            .GetTypes()
            .Where(type => type.Namespace is null
                || !type.Namespace.StartsWith("FlurNetz.Worker", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToArray();
}
