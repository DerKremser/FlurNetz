using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Domain;

namespace FlurNetz.Modules.Progression.Application;

/// <summary>
/// Definiert die modulinterne Persistenzgrenze für Community-Progressionen.
/// </summary>
/// <remarks>
/// Die XP-Vergabe ist bewusst eine atomare Port-Operation. Lesen, Sperren,
/// Rehydrieren, Domain-Mutation und Speichern dürfen nicht in getrennte
/// Transaktionen aufgeteilt werden, weil sonst parallele Vergaben verloren gehen könnten.
/// </remarks>
public interface ICommunityProgressionStore
{
    /// <summary>
    /// Vergibt XP atomar und liefert den neuen Gesamtwert nach erfolgreichem Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Die positive XP-Menge.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue Gesamtwert nach erfolgreicher Persistierung.</returns>
    Task<ExperiencePoints> GrantExperienceAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vergibt XP innerhalb einer bereits bestehenden ADO.NET-Transaktion.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Die positive XP-Menge.</param>
    /// <param name="connection">Die gemeinsame geöffnete Datenbankverbindung.</param>
    /// <param name="transaction">Die gemeinsame Datenbanktransaktion.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Der neue Gesamtwert nach erfolgreichem Business-Write.</returns>
    /// <remarks>
    /// Dieser Port verwendet nur neutrale ADO.NET-Verträge, damit ein Inbox-Handler seine
    /// bereits laufende Messaging-Transaktion durchreichen kann. Der Store führt hier
    /// keinen eigenen Commit aus.
    /// </remarks>
    Task<ExperiencePoints> GrantExperienceAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt einen Progressionszustand über seine Community-Identität.
    /// </summary>
    /// <param name="communityIdentityId">Die gesuchte interne Identität.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Lesevorgangs.</param>
    /// <returns>Der Zustand oder <see langword="null"/>, wenn keine Zeile existiert.</returns>
    Task<CommunityProgression?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);
}
