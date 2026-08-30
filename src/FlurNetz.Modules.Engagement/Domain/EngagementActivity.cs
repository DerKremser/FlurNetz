using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Engagement.Domain;

/// <summary>
/// Repräsentiert eine einer internen Community-Identität zugeordnete Engagement-Aktivität.
/// </summary>
/// <remarks>
/// Die Foundation hält absichtlich nur die beiden fachlich notwendigen Identifiers. Die
/// konkrete Aktivitätsart, Zeitangaben und Quelldaten werden erst mit einem realen Recording-
/// Use-Case festgelegt. Externe Plattformidentitäten müssen vor dieser Domain-Grenze auf die
/// zentrale <see cref="CommunityIdentityId"/> aufgelöst worden sein.
/// </remarks>
public sealed class EngagementActivity
{
    private EngagementActivity(
        EngagementActivityId id,
        CommunityIdentityId communityIdentityId)
    {
        Id = id;
        CommunityIdentityId = communityIdentityId;
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
    /// Erstellt eine gültige Engagement-Aktivität für eine aufgelöste Community-Identität.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung der Engagement-Aktivität.</param>
    /// <param name="communityIdentityId">Die nicht leere interne Community-Identity-ID.</param>
    /// <returns>Eine unveränderliche Engagement-Aktivität.</returns>
    /// <exception cref="ArgumentException">
    /// Wenn eine der beiden übergebenen Kennungen leer ist.
    /// </exception>
    public static EngagementActivity Create(
        EngagementActivityId id,
        CommunityIdentityId communityIdentityId)
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

        return new EngagementActivity(id, communityIdentityId);
    }
}
