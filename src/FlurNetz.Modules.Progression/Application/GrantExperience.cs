using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Domain;

namespace FlurNetz.Modules.Progression.Application;

/// <summary>
/// Vergibt Experience Points über die atomare Persistenzgrenze des Progression-Moduls.
/// </summary>
/// <remarks>
/// Der Use Case enthält keine SQL-, PostgreSQL- oder Transaktionslogik. Die fachliche
/// XP-Validierung bleibt in <see cref="CommunityProgression.GrantExperience(long)"/>;
/// die atomare Umsetzung liegt hinter <see cref="ICommunityProgressionStore"/>.
/// </remarks>
public sealed class GrantExperience
{
    private readonly ICommunityProgressionStore store;

    /// <summary>
    /// Erstellt den Use Case mit der modulbezogenen Persistenzgrenze.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Progressionen.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public GrantExperience(ICommunityProgressionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Vergibt XP und liefert den neuen Gesamtwert nach erfolgreichem Commit.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="amount">Die positive XP-Menge.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Der neue persistierte Experience-Points-Gesamtwert.</returns>
    public Task<ExperiencePoints> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        return store.GrantExperienceAsync(communityIdentityId, amount, cancellationToken);
    }
}
