using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Definiert die modulinterne atomare Persistenzgrenze für Community-Titelzustände.
/// </summary>
/// <remarks>
/// Der synchrone Callback enthält ausschließlich Domain-Logik. Externe I/O kann dadurch
/// nicht innerhalb der offenen Titles-Transaktion ausgeführt werden.
/// </remarks>
public interface ICommunityTitlesStore
{
    /// <summary>
    /// Lädt, mutiert und persistiert einen Community-Titelzustand atomar.
    /// </summary>
    /// <typeparam name="TResult">Der fachliche Rückgabewert der Domain-Operation.</typeparam>
    /// <param name="communityIdentityId">Die interne Community-Identität.</param>
    /// <param name="operation">Die synchrone Domain-Operation.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Das Ergebnis der Domain-Operation nach erfolgreichem Commit.</returns>
    Task<TResult> ExecuteAsync<TResult>(
        CommunityIdentityId communityIdentityId,
        Func<CommunityTitles, TResult> operation,
        CancellationToken cancellationToken = default);
}
