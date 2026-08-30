using System.Reflection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Sichert den bewusst kleinen Umfang und die Abhängigkeitsgrenze der Rewards-Foundation.
/// </summary>
public sealed class RewardsArchitectureTests
{
    private static Assembly RewardsImplementationAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Rewards");

    private static Assembly RewardsContractsAssembly =>
        ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Rewards.Contracts");

    [Fact]
    public void RewardsImplementationReferencesOnlyItsContractsAndIdentityContracts()
    {
        var references = GetReferencedAssemblyNames(RewardsImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Rewards.Contracts",
            "FlurNetz.Modules.Identity.Contracts"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.All(references, reference => Assert.Contains(reference, allowedReferences));
    }

    [Fact]
    public void RewardsHasNoForbiddenForeignModuleReferences()
    {
        var references = GetReferencedAssemblyNames(RewardsImplementationAssembly);
        var forbiddenReferences = new[]
        {
            "FlurNetz.Modules.Identity",
            "FlurNetz.Modules.Economy",
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Modules.Progression",
            "FlurNetz.Modules.Progression.Contracts",
            "FlurNetz.Modules.Engagement",
            "FlurNetz.Modules.Engagement.Contracts",
            "FlurNetz.Modules.Inventory",
            "FlurNetz.Modules.Titles",
            "FlurNetz.Modules.Achievements",
            "FlurNetz.Modules.Shop",
            "FlurNetz.Messaging",
            "FlurNetz.Persistence",
            "FlurNetz.Worker",
            "FlurNetz.Api"
        };

        Assert.DoesNotContain(references, reference => forbiddenReferences.Contains(reference, StringComparer.Ordinal));
    }

    [Fact]
    public void RewardsContractsRemainEmpty()
    {
        Assert.Empty(RewardsContractsAssembly.GetExportedTypes());
    }

    [Fact]
    public void RewardsDomainTypesRemainInTheImplementationAssembly()
    {
        var expectedTypeNames = new[]
        {
            "FlurNetz.Modules.Rewards.Domain.RewardDefinitionId",
            "FlurNetz.Modules.Rewards.Domain.RewardPackageId",
            "FlurNetz.Modules.Rewards.Domain.RewardGrantId",
            "FlurNetz.Modules.Rewards.Domain.RewardDefinition",
            "FlurNetz.Modules.Rewards.Domain.EconomyBalanceRewardDefinition",
            "FlurNetz.Modules.Rewards.Domain.RewardPackage",
            "FlurNetz.Modules.Rewards.Domain.RewardSource",
            "FlurNetz.Modules.Rewards.Domain.RewardGrant"
        };

        foreach (var typeName in expectedTypeNames)
        {
            Assert.NotNull(RewardsImplementationAssembly.GetType(typeName));
            Assert.Null(RewardsContractsAssembly.GetType(typeName));
        }
    }

    [Fact]
    public void RewardsFoundationContainsNoOutOfScopeProductiveTypes()
    {
        var forbiddenNameParts = new[]
        {
            "Experience",
            "XpReward",
            "Currency",
            "CoinReward",
            "CoinsReward",
            "ItemReward",
            "TitleReward",
            "AchievementReward",
            "Executor",
            "Engine",
            "Repository",
            "Store",
            "Migration",
            "Event",
            "Inbox",
            "Outbox",
            "Status"
        };

        var forbiddenTypes = RewardsImplementationAssembly
            .GetTypes()
            .Where(type => forbiddenNameParts.Any(namePart =>
                type.Name.Contains(namePart, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
