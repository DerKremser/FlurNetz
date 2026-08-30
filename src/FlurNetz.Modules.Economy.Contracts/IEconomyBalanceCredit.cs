using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Contracts;

/// <summary>
/// Stellt die neutrale Fähigkeit bereit, einen positiven Economy-Betrag zu verbuchen.
/// </summary>
/// <remarks>
/// Die Fähigkeit kennt weder Rewards noch einen anderen Aufrufer. Verbindung und
/// Transaktion werden vom Kompositor bereitgestellt, damit eine fachliche Mutation eines
/// anderen Moduls und diese Economy-Mutation dieselbe atomare Datenbankgrenze teilen können.
/// Der Contract verwendet deshalb bewusst nur ADO.NET-Basistypen und keine PostgreSQL-
/// oder Economy-Implementierungsdetails.
/// </remarks>
public interface IEconomyBalanceCredit
{
    /// <summary>
    /// Schreibt einen positiven Betrag innerhalb der bereitgestellten Transaktion gut.
    /// </summary>
    /// <param name="communityIdentityId">Die zentrale interne Community-Identität.</param>
    /// <param name="amount">Der positive gutzuschreibende Betrag.</param>
    /// <param name="connection">Die geöffnete gemeinsame Datenbankverbindung.</param>
    /// <param name="transaction">Die zu <paramref name="connection"/> gehörende Transaktion.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Eine Aufgabe, die nach erfolgreichem Economy-Write abgeschlossen ist.</returns>
    Task CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
