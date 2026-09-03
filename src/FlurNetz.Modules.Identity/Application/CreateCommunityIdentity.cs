using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using System.Data.Common;

namespace FlurNetz.Modules.Identity.Application;

/// <summary>
/// Erzeugt eine neue interne Community-Identität und speichert sie dauerhaft.
/// </summary>
/// <remarks>
/// Der Use Case vergibt ausschließlich die interne FlurNetz-Kennung. Externe Plattform-IDs
/// gehören in eine spätere Auflösungsgrenze und dürfen die zentrale Identität nicht bestimmen.
/// </remarks>
public sealed class CreateCommunityIdentity : ICommunityIdentityCreator
{
    private readonly ICommunityIdentityRepository repository;

    /// <summary>
    /// Erstellt den Use Case mit seiner moduleigenen Persistenzgrenze.
    /// </summary>
    /// <param name="repository">Persistenzgrenze für Community-Identitäten.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="repository"/> fehlt.</exception>
    public CreateCommunityIdentity(ICommunityIdentityRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
    }

    /// <summary>
    /// Erzeugt und persistiert eine neue Community-Identität.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Persistierung.</param>
    /// <returns>Die stabile interne Kennung der neu gespeicherten Identität.</returns>
    public async Task<CommunityIdentityId> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var id = CommunityIdentityId.New();
        var identity = CommunityIdentity.Create(id);

        await repository.AddAsync(identity, cancellationToken).ConfigureAwait(false);

        return id;
    }

    public async Task<CommunityIdentityId> CreateAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var id = CommunityIdentityId.New();
        await repository.AddAsync(
                CommunityIdentity.Create(id),
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);
        return id;
    }
}
