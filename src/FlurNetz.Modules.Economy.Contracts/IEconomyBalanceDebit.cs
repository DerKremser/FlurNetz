using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Contracts;

/// <summary>
/// Stellt die neutrale Fähigkeit bereit, einen positiven Economy-Betrag abzubuchen.
/// </summary>
/// <remarks>
/// Die Fähigkeit kennt keinen fachlichen Aufrufer. Der Aufrufer stellt Verbindung und
/// Transaktion bereit, damit Economy-Debit und dessen eigene Business-Writes innerhalb
/// derselben atomaren Datenbankgrenze ausgeführt werden können.
/// </remarks>
public interface IEconomyBalanceDebit
{
    /// <summary>
    /// Bucht einen positiven Betrag innerhalb der bereitgestellten Transaktion ab.
    /// </summary>
    Task DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
