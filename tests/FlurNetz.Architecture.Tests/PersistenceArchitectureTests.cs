using System.Reflection;
using FlurNetz.Persistence.Configuration;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die technische Isolation der Persistence-Foundation von Fachmodulen und Hosts.
/// </summary>
public sealed class PersistenceArchitectureTests
{
    // Persistence stellt nur die technische Datenbankbasis bereit; Fachmodule und Adapter
    // dürfen nicht als transitive Kopplung in diese untere Schicht gelangen.
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
        // Verhindert, dass Persistence die Abhängigkeitsrichtung zu Messaging, API, Worker oder Fachmodulen umkehrt.
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
        // Ein konsistenter Namespace hält die technische API von versehentlich exportierten Fremdtypen getrennt.
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
        // Die Foundation liefert Verbindungen, Transaktionen und Migrationen; generische Repositories
        // würden bereits jetzt fachliche Datenzugriffe und ein ungewolltes Abstraktionsmodell festlegen.
        var forbiddenTypes = PersistenceAssembly
            .GetExportedTypes()
            .Where(type => type.IsGenericType
                && type.Name.Split('`')[0] is "IRepository" or "Repository" or "GenericRepository" or "CrudRepository")
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }
}
