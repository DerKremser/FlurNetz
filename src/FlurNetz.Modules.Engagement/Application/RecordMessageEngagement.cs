using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Engagement.Application;

/// <summary>
/// Zeichnet eine normalisierte Message-Aktivität für eine bereits aufgelöste Community-Identität auf.
/// </summary>
/// <remarks>
/// Der Aufrufer liefert nur die interne <see cref="CommunityIdentityId"/>. Zeitpunkt und
/// Aktivitätstyp werden für diesen Slice intern bestimmt; Nachrichtentext und externe
/// Plattformdaten sind deshalb weder Eingabe noch Bestandteil des Use Cases.
/// </remarks>
public sealed class RecordMessageEngagement
{
    private readonly IEngagementActivityRepository repository;
    private readonly IClock clock;

    /// <summary>
    /// Erstellt den Recording-Use-Case mit seinem Persistenz-Port und seiner UTC-Zeitquelle.
    /// </summary>
    /// <param name="repository">Der Engagement-eigene Persistenz-Port.</param>
    /// <param name="clock">Die testbare UTC-Zeitquelle.</param>
    /// <exception cref="ArgumentNullException">Wenn eine Abhängigkeit fehlt.</exception>
    public RecordMessageEngagement(
        IEngagementActivityRepository repository,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        this.repository = repository;
        this.clock = clock;
    }

    /// <summary>
    /// Erzeugt und persistiert eine Message-Aktivität.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Community-Identity-ID.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Persistierung.</param>
    /// <returns>Die Kennung der neu aufgezeichneten Aktivität.</returns>
    /// <exception cref="ArgumentException">
    /// Wenn die Community-Identity-ID ungültig ist oder die Zeitquelle keinen UTC-Zeitpunkt liefert.
    /// </exception>
    public async Task<EngagementActivityId> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var id = EngagementActivityId.New();
        var activity = EngagementActivity.CreateMessage(id, communityIdentityId, clock.UtcNow);

        await repository.AddAsync(activity, cancellationToken).ConfigureAwait(false);

        return id;
    }
}
