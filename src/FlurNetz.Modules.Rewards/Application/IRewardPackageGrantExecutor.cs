using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Definiert die eine atomare Infrastrukturgrenze für Package-Grants.
/// </summary>
/// <remarks>
/// Die Transaktions- und SQL-Orchestrierung bleibt hinter diesem gezielten Port. Dadurch
/// enthält der Use Case keine Persistenzdetails und es entsteht keine generische Reward-Engine.
/// </remarks>
public interface IRewardPackageGrantExecutor
{
    /// <summary>
    /// Führt ein Reward-Package idempotent und vollständig atomar aus.
    /// </summary>
    /// <param name="rewardPackageId">Das auszuführende Package.</param>
    /// <param name="communityIdentityId">Die zentrale Empfängeridentität.</param>
    /// <param name="source">Die fachliche Ursache des Grants.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Transaktion.</param>
    /// <returns>Das fachliche Grant-Ergebnis.</returns>
    Task<RewardPackageGrantOutcome> ExecuteAsync(
        RewardPackageId rewardPackageId,
        CommunityIdentityId communityIdentityId,
        RewardSource source,
        CancellationToken cancellationToken = default);
}
