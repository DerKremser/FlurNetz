using System.Reflection;
using FlurNetz.Modules.Rewards.Migrations;

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
    public void RewardsImplementationReferencesOnlyItsApprovedProjects()
    {
        var references = GetReferencedAssemblyNames(RewardsImplementationAssembly);
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlurNetz.Modules.Rewards.Contracts",
            "FlurNetz.Modules.Identity.Contracts",
            "FlurNetz.Modules.Economy.Contracts",
            "FlurNetz.Persistence"
        };

        Assert.Contains("FlurNetz.Modules.Identity.Contracts", references);
        Assert.Contains("FlurNetz.Modules.Economy.Contracts", references);
        Assert.Contains("FlurNetz.Persistence", references);
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
            "FlurNetz.Modules.Progression",
            "FlurNetz.Modules.Progression.Contracts",
            "FlurNetz.Modules.Engagement",
            "FlurNetz.Modules.Engagement.Contracts",
            "FlurNetz.Modules.Inventory",
            "FlurNetz.Modules.Titles",
            "FlurNetz.Modules.Achievements",
            "FlurNetz.Modules.Shop",
            "FlurNetz.Messaging",
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
            "RewardEngine",
            "RewardPipeline",
            "RewardExecutorFactory",
            "RewardProvider",
            "RewardStrategy",
            "IRewardComponent",
            "IRewardEffect",
            "RewardComponent",
            "RewardTarget",
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

    [Fact]
    public void RewardsMigrationIsOwnedAndHasNoCrossModuleForeignKey()
    {
        var migration = Assert.Single(new RewardsMigrationSource().GetMigrations());

        Assert.Equal("Rewards", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateRewardConfigurationAndGrants", migration.Name);
        Assert.Contains("reward_definitions", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("reward_packages", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("reward_grants", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (source_type, source_id, reward_definition_id)", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("community_identities", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community_economies", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(referencedAssembly => referencedAssembly.Name)
        .Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal))
        .Select(name => name!)
        .ToArray();
}
