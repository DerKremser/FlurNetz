using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Engagement.Domain;

/// <summary>
/// Repräsentiert eine einer internen Community-Identität zugeordnete Engagement-Aktivität.
/// </summary>
/// <remarks>
/// Die Aktivität bildet aktuell ausschließlich eine Message auf Basis der beiden Identifiers
/// und ihres UTC-Zeitpunkts ab. Nachrichtentext und Plattformdaten gehören nicht in diesen
/// normalisierten Kern. Externe Plattformidentitäten müssen vor dieser Domain-Grenze auf die
/// zentrale <see cref="CommunityIdentityId"/> aufgelöst worden sein.
/// </remarks>
public sealed class EngagementActivity
{
    private EngagementActivity(
        EngagementActivityId id,
        CommunityIdentityId communityIdentityId,
        EngagementActivityType type,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        CommunityIdentityId = communityIdentityId;
        Type = type;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// Liefert die unveränderliche Kennung dieser Engagement-Aktivität.
    /// </summary>
    public EngagementActivityId Id { get; }

    /// <summary>
    /// Liefert die interne Community-Identität, der diese Aktivität zugeordnet ist.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert die konkrete normalisierte Aktivitätsart.
    /// </summary>
    public EngagementActivityType Type { get; }

    /// <summary>
    /// Liefert den fachlichen Zeitpunkt der Aktivität in UTC.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Erstellt eine gültige Message-Aktivität für eine aufgelöste Community-Identität.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung der Engagement-Aktivität.</param>
    /// <param name="communityIdentityId">Die nicht leere interne Community-Identity-ID.</param>
    /// <param name="occurredAtUtc">Der Zeitpunkt der Aufzeichnung mit UTC-Offset.</param>
    /// <returns>Eine unveränderliche Message-Aktivität.</returns>
    /// <exception cref="ArgumentException">
    /// Wenn eine Kennung leer ist oder <paramref name="occurredAtUtc"/> keinen UTC-Offset besitzt.
    /// </exception>
    public static EngagementActivity CreateMessage(
        EngagementActivityId id,
        CommunityIdentityId communityIdentityId,
        DateTimeOffset occurredAtUtc)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Engagement-Aktivität benötigt eine nicht leere Engagement-Aktivitäts-ID.",
                nameof(id));
        }

        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Engagement-Aktivität benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Der Zeitpunkt einer Engagement-Aktivität muss in UTC angegeben werden.",
                nameof(occurredAtUtc));
        }

        return new EngagementActivity(
            id,
            communityIdentityId,
            EngagementActivityType.Message,
            occurredAtUtc);
    }
}
