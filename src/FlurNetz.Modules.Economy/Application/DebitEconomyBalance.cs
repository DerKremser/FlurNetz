using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Application;

/// <summary>
/// Bucht über die atomare Persistenzgrenze des Economy-Moduls einen Betrag ab.
/// </summary>
/// <remarks>
/// Der Use Case enthält keine SQL- oder Transaktionslogik. Die Domain entscheidet,
/// ob der vorhandene Saldo ausreicht; die fachliche Insufficient-Balance-Exception
/// bleibt für den Aufrufer sichtbar.
/// </remarks>
public sealed class DebitEconomyBalance
{
    private readonly ICommunityEconomyStore store;

    /// <summary>
    /// Erstellt den Use Case mit der modulbezogenen Persistenzgrenze.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Economy-Zustände.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public DebitEconomyBalance(ICommunityEconomyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Bucht einen Betrag ab und liefert den neuen Saldo nach erfolgreichem Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Der positive abzubuchende Betrag.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue persistierte Economy-Saldo.</returns>
    /// <exception cref="InsufficientEconomyBalanceException">Wenn der Saldo nicht ausreicht.</exception>
    public Task<EconomyBalance> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        return store.DebitAsync(communityIdentityId, amount, cancellationToken);
    }
}
