using System.Reflection;
using FlurNetz.Messaging.Integration;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert die technische Grenze und den fachlich neutralen Inhalt der Messaging Foundation.
/// </summary>
public sealed class MessagingArchitectureTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "FlurNetz.Engagement",
        "FlurNetz.Progression",
        "FlurNetz.Economy",
        "FlurNetz.Rewards",
        "FlurNetz.Inventory",
        "FlurNetz.Titles",
        "FlurNetz.Achievements",
        "FlurNetz.Shop",
        "FlurNetz.Notifications",
        "FlurNetz.Automation",
        "FlurNetz.Overlay",
        "FlurNetz.Identity",
        "FlurNetz.Administration",
        "FlurNetz.Integrations",
        "FlurNetz.Api",
        "FlurNetz.Worker"
    ];

    private static readonly string[] ForbiddenTypeNames =
    [
        "UserCreatedEvent",
        "XpChangedEvent",
        "CoinsChangedEvent",
        "RewardGrantedEvent",
        "AchievementUnlockedEvent",
        "ShopItemPurchasedEvent",
        "IRepository",
        "Repository",
        "GenericRepository"
    ];

    private static Assembly MessagingAssembly => typeof(IntegrationEventEnvelope).Assembly;

    [Fact]
    public void MessagingHasNoForbiddenProjectReferences()
    {
        var forbiddenReferences = MessagingAssembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null
                && ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void MessagingUsesConsistentNamespaces()
    {
        var invalidTypes = MessagingAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace is null
                || !type.Namespace.StartsWith("FlurNetz.Messaging.", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }

    [Fact]
    public void MessagingContainsNoGenericRepositoriesOrKnownDomainEvents()
    {
        var invalidTypes = MessagingAssembly
            .GetExportedTypes()
            .Where(type => ForbiddenTypeNames.Contains(type.Name.Split('`')[0], StringComparer.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(invalidTypes);
    }
}
