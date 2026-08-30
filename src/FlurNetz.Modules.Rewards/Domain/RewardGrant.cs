using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Repräsentiert den fachlichen Grant-Record genau einer ausgeführten Reward-Definition.
/// </summary>
/// <remarks>
/// Der Grant verweist bewusst auf eine einzelne <see cref="RewardDefinitionId"/> und nicht
/// nur auf ein Package. Die spätere verbindliche Eindeutigkeit kann dadurch pro Quelle und
/// Definition gelten: <c>SourceType</c> plus <c>SourceId</c> plus <c>RewardDefinitionId</c>.
/// Der Record enthält noch keinen Status, Zeitstempel, Package-Verweis oder Persistenzaspekt.
/// </remarks>
public sealed class RewardGrant
{
    private RewardGrant(
        RewardGrantId id,
        CommunityIdentityId communityIdentityId,
        RewardDefinitionId rewardDefinitionId,
        RewardSource source)
    {
        Id = id;
        CommunityIdentityId = communityIdentityId;
        RewardDefinitionId = rewardDefinitionId;
        Source = source;
    }

    /// <summary>
    /// Liefert die unveränderliche Kennung des Grant-Records.
    /// </summary>
    public RewardGrantId Id { get; }

    /// <summary>
    /// Liefert die zentrale interne Identität des Reward-Empfängers.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert die genau einem Grant zugeordnete Reward-Definition.
    /// </summary>
    public RewardDefinitionId RewardDefinitionId { get; }

    /// <summary>
    /// Liefert die fachliche Herkunft dieses Grants.
    /// </summary>
    public RewardSource Source { get; }

    /// <summary>
    /// Erstellt einen gültigen Grant-Record für eine einzelne Reward-Definition.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung des Grant-Records.</param>
    /// <param name="communityIdentityId">Die nicht leere zentrale Empfängeridentität.</param>
    /// <param name="rewardDefinitionId">Die nicht leere ausgeführte Reward-Definition.</param>
    /// <param name="source">Die gültige fachliche Herkunft des Grants.</param>
    /// <returns>Ein unveränderlicher fachlicher Grant-Record.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="source"/> fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn eine Kennung leer ist.</exception>
    public static RewardGrant Create(
        RewardGrantId id,
        CommunityIdentityId communityIdentityId,
        RewardDefinitionId rewardDefinitionId,
        RewardSource? source)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Reward-Grant benötigt eine nicht leere ID.",
                nameof(id));
        }

        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Reward-Grant benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }

        if (rewardDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Reward-Grant benötigt eine nicht leere Reward-Definitions-ID.",
                nameof(rewardDefinitionId));
        }

        ArgumentNullException.ThrowIfNull(source);

        return new RewardGrant(
            id,
            communityIdentityId,
            rewardDefinitionId,
            source);
    }
}
