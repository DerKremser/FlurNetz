using System.Data.Common;
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
    Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schreibt einen positiven Betrag innerhalb einer bereits bestehenden Transaktion gut.
    /// </summary>
    /// <remarks>
    /// Dieser Overload führt keinen Commit aus. Dadurch kann eine übergeordnete fachliche
    /// Operation Economy gemeinsam mit eigenen Writes atomar bestätigen oder zurückrollen.
    /// </remarks>
    Task<EconomyBalance> CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bucht einen positiven Betrag atomar ab und liefert den neuen Saldo nach Commit.
    /// </summary>
    /// <exception cref="InsufficientEconomyBalanceException">Wenn der Saldo nicht ausreicht.</exception>
    Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bucht einen positiven Betrag innerhalb einer bereits bestehenden Transaktion ab.
    /// </summary>
    /// <remarks>
    /// Dieser Overload führt keinen Commit aus. Die fachliche Validierung und Row-Lock-
    /// Semantik bleiben identisch zum normalen Debit-Pfad.
    /// </remarks>
    Task<EconomyBalance> DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt einen Economy-Zustand ohne beim Lesen einen fehlenden Zustand anzulegen.
    /// </summary>
    Task<CommunityEconomy?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);
}
