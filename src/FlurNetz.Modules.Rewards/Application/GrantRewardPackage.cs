using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Delegiert die atomare Ausführung eines Reward-Packages an die spezifische Infrastrukturgrenze.
/// </summary>
/// <remarks>
/// Der Use Case validiert nur die Identitäts- und Source-Eingaben. SQL, Grant-Reservierung,
/// Economy-Aufruf sowie Commit und Rollback gehören vollständig zum Executor.
/// </remarks>
public sealed class GrantRewardPackage
{
    private readonly IRewardPackageGrantExecutor executor;

    /// <summary>
    /// Erstellt den Package-Grant-Use-Case.
    /// </summary>
    /// <param name="executor">Die atomare Package-Ausführungsgrenze.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="executor"/> fehlt.</exception>
    public GrantRewardPackage(IRewardPackageGrantExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        this.executor = executor;
    }

    /// <summary>
    /// Gewährt ein Package idempotent für eine fachliche Quelle.
    /// </summary>
    /// <param name="rewardPackageId">Die gültige Package-ID.</param>
    /// <param name="communityIdentityId">Die zentrale interne Empfängeridentität.</param>
    /// <param name="source">Die gültige fachliche Reward-Quelle.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns><see cref="RewardPackageGrantOutcome.Granted"/> oder <see cref="RewardPackageGrantOutcome.AlreadyGranted"/>.</returns>
    public Task<RewardPackageGrantOutcome> ExecuteAsync(
        RewardPackageId rewardPackageId,
        CommunityIdentityId communityIdentityId,
        RewardSource source,
        CancellationToken cancellationToken = default)
    {
        var validRewardPackageId = RewardPackageId.Create(rewardPackageId.Value);
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        ArgumentNullException.ThrowIfNull(source);

        return executor.ExecuteAsync(
            validRewardPackageId,
            validCommunityIdentityId,
            source,
            cancellationToken);
    }
}
