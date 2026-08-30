using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Konfiguriert und persistiert eine Economy-Balance-Reward-Definition.
/// </summary>
/// <remarks>
/// Der Use Case erzeugt keine Economy-Buchung. Er speichert nur die gewünschte spätere
/// Wirkung; die tatsächliche Zielmodul-Mutation gehört in den Grant-Executor.
/// </remarks>
public sealed class CreateEconomyBalanceRewardDefinition
{
    private readonly IRewardCatalogStore store;

    /// <summary>
    /// Erstellt den Definitions-Konfigurations-Use-Case.
    /// </summary>
    /// <param name="store">Der gezielte Rewards-Katalog-Store.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public CreateEconomyBalanceRewardDefinition(IRewardCatalogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Erzeugt eine neue Definition mit einem positiven Betrag und persistiert sie.
    /// </summary>
    /// <param name="amount">Der positive Betrag der späteren Economy-Balance-Wirkung.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Persistierung.</param>
    /// <returns>Die neue Reward-Definitions-ID.</returns>
    public async Task<RewardDefinitionId> ExecuteAsync(
        long amount,
        CancellationToken cancellationToken = default)
    {
        var definition = EconomyBalanceRewardDefinition.Create(
            RewardDefinitionId.New(),
            amount);

        await store.AddDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        return definition.Id;
    }
}
