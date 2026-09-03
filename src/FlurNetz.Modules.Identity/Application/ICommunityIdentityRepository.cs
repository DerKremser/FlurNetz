using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using System.Data.Common;

namespace FlurNetz.Modules.Identity.Application;

/// <summary>
/// Definiert die minimale Persistenzgrenze für die interne Community-Identität.
/// </summary>
/// <remarks>
/// Der Vertrag bleibt innerhalb der Identity-Implementierung, damit andere Module nicht
/// an eine fachliche Schreib- oder Leseabstraktion gekoppelt werden. Die konkrete SQL- und
/// Transaktionsumsetzung liegt im Identity-Persistence-Adapter.
/// </remarks>
public interface ICommunityIdentityRepository
{
    /// <summary>
    /// Speichert eine neue Community-Identität.
    /// </summary>
    /// <param name="identity">Die bereits gültige interne Community-Identität.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Ein Task, der nach dem Commit der Speicherung abgeschlossen ist.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="identity"/> fehlt.</exception>
    Task AddAsync(CommunityIdentity identity, CancellationToken cancellationToken = default);

    Task AddAsync(
        CommunityIdentity identity,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt eine Community-Identität über ihre interne Kennung.
    /// </summary>
    /// <param name="id">Die nicht leere interne Community-Identity-ID.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Die gefundene Identität oder <see langword="null"/>, wenn keine Zeile existiert.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="id"/> leer ist.</exception>
    Task<CommunityIdentity?> GetByIdAsync(
        CommunityIdentityId id,
        CancellationToken cancellationToken = default);
}
