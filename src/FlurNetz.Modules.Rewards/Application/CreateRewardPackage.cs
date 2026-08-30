using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Konfiguriert und persistiert ein verpflichtendes Reward-Package.
/// </summary>
/// <remarks>
/// Die Domain validiert die Package-Invarianten. Der Store prüft die Referenzen erneut in
/// seiner eigenen Transaktion, damit Package-Zeile und Membership niemals getrennt sichtbar
/// werden.
/// </remarks>
public sealed class CreateRewardPackage
{
    private readonly IRewardCatalogStore store;

    /// <summary>
    /// Erstellt den Package-Konfigurations-Use-Case.
    /// </summary>
    /// <param name="store">Der gezielte Rewards-Katalog-Store.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public CreateRewardPackage(IRewardCatalogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Erzeugt ein Package aus vorhandenen Definitionen und persistiert es atomar.
    /// </summary>
    /// <param name="rewardDefinitionIds">Die mindestens eine Definition enthaltende Sammlung.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Persistierung.</param>
    /// <returns>Die neue Reward-Package-ID.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="rewardDefinitionIds"/> fehlt.</exception>
    /// <exception cref="RewardDefinitionNotFoundException">Wenn eine Definition unbekannt ist.</exception>
    public async Task<RewardPackageId> ExecuteAsync(
        IEnumerable<RewardDefinitionId> rewardDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rewardDefinitionIds);

        var package = RewardPackage.Create(
            RewardPackageId.New(),
            rewardDefinitionIds.ToArray());

        var missingDefinitionIds = await store
            .FindMissingDefinitionIdsAsync(package.RewardDefinitionIds, cancellationToken)
            .ConfigureAwait(false);

        if (missingDefinitionIds.Count != 0)
        {
            throw new RewardDefinitionNotFoundException(missingDefinitionIds);
        }

        await store.AddPackageAsync(package, cancellationToken).ConfigureAwait(false);
        return package.Id;
    }
}
