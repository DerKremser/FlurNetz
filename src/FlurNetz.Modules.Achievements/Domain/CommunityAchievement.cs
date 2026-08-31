using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Achievements.Domain;

/// <summary>
/// Modelliert ein dauerhaft für eine Community erreichtes Achievement.
/// </summary>
/// <remarks>
/// Der Unlock-Zeitpunkt ist unveränderlich und muss bereits als UTC-Zeitpunkt mit Offset null
/// vorliegen. Die Existenz der Community-Identität wird in diesem Modul nicht geprüft.
/// </remarks>
public sealed class CommunityAchievement
{
    private CommunityAchievement(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        DateTimeOffset unlockedAtUtc)
    {
        CommunityIdentityId = communityIdentityId;
        AchievementDefinitionId = achievementDefinitionId;
        UnlockedAtUtc = unlockedAtUtc;
    }

    /// <summary>
    /// Liefert die interne Community-Identität des Achievements.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert die Achievement-Definition, die erreicht wurde.
    /// </summary>
    public AchievementDefinitionId AchievementDefinitionId { get; }

    /// <summary>
    /// Liefert den unveränderlichen UTC-Zeitpunkt des erfolgreichen Unlocks.
    /// </summary>
    public DateTimeOffset UnlockedAtUtc { get; }

    /// <summary>
    /// Erstellt ein gültiges Community-Achievement.
    /// </summary>
    /// <param name="communityIdentityId">Die strukturell gültige Community-Identität.</param>
    /// <param name="achievementDefinitionId">Die strukturell gültige Definition.</param>
    /// <param name="unlockedAtUtc">Der UTC-Zeitpunkt mit Offset null.</param>
    /// <returns>Ein unveränderliches Community-Achievement.</returns>
    public static CommunityAchievement Create(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        DateTimeOffset unlockedAtUtc)
    {
        return CreateValidated(communityIdentityId, achievementDefinitionId, unlockedAtUtc);
    }

    /// <summary>
    /// Rekonstruiert ein persistiertes Community-Achievement.
    /// </summary>
    /// <param name="communityIdentityId">Die persistierte Community-Identität.</param>
    /// <param name="achievementDefinitionId">Die persistierte Definition.</param>
    /// <param name="unlockedAtUtc">Der persistierte UTC-Zeitpunkt.</param>
    /// <returns>Das rekonstruierte, gültige Community-Achievement.</returns>
    public static CommunityAchievement Rehydrate(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        DateTimeOffset unlockedAtUtc)
    {
        return CreateValidated(communityIdentityId, achievementDefinitionId, unlockedAtUtc);
    }

    private static CommunityAchievement CreateValidated(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        DateTimeOffset unlockedAtUtc)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validAchievementDefinitionId = AchievementDefinitionId.Create(achievementDefinitionId.Value);

        if (unlockedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Der Unlock-Zeitpunkt muss kanonisch in UTC mit Offset null vorliegen.",
                nameof(unlockedAtUtc));
        }

        return new CommunityAchievement(
            validCommunityIdentityId,
            validAchievementDefinitionId,
            unlockedAtUtc);
    }
}
