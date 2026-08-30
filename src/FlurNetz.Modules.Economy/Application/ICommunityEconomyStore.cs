using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Application;

/// <summary>
/// Definiert die modulinterne Persistenzgrenze für Community-Economy-Zustände.
/// </summary>
/// <remarks>
/// Credit und Debit sind vollständige atomare Read/Modify/Write-Operationen. Die
/// Domain wird innerhalb derselben Datenbanktransaktion rehydriert und mutiert,
/// damit parallele Vorgänge keine fachlichen Änderungen verlieren können.
/// </remarks>
public interface ICommunityEconomyStore
{
    /// <summary>
    /// Schreibt einen positiven Betrag atomar gut und liefert den neuen Saldo nach Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Der positive gutzuschreibende Betrag.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue Economy-Saldo nach erfolgreichem Persistieren.</returns>
    Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bucht einen positiven Betrag atomar ab und liefert den neuen Saldo nach Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Der positive abzubuchende Betrag.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue Economy-Saldo nach erfolgreichem Persistieren.</returns>
    /// <exception cref="InsufficientEconomyBalanceException">Wenn der Saldo nicht ausreicht.</exception>
    Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt einen Economy-Zustand ohne beim Lesen einen fehlenden Zustand anzulegen.
    /// </summary>
    /// <param name="communityIdentityId">Die gesuchte interne Identität.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Lesevorgangs.</param>
    /// <returns>Der Zustand oder <see langword="null"/>, wenn keine Zeile existiert.</returns>
    Task<CommunityEconomy?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);
}
