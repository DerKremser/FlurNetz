using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Application;

/// <summary>
/// Schreibt über die atomare Persistenzgrenze des Economy-Moduls einen Betrag gut.
/// </summary>
/// <remarks>
/// Der Use Case enthält keine SQL- oder Transaktionslogik. Die fachliche Validierung
/// bleibt in <see cref="CommunityEconomy.Credit(long)"/>; der Store hält den gesamten
/// Persistenzvorgang atomar.
/// </remarks>
public sealed class CreditEconomyBalance
{
    private readonly ICommunityEconomyStore store;

    /// <summary>
    /// Erstellt den Use Case mit der modulbezogenen Persistenzgrenze.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Economy-Zustände.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public CreditEconomyBalance(ICommunityEconomyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Schreibt einen Betrag gut und liefert den neuen Saldo nach erfolgreichem Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Der positive gutzuschreibende Betrag.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue persistierte Economy-Saldo.</returns>
    public Task<EconomyBalance> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        return store.CreditAsync(communityIdentityId, amount, cancellationToken);
    }
}
