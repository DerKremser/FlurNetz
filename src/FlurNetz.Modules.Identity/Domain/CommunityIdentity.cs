using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Identity.Domain;

/// <summary>
/// Repräsentiert die minimale zentrale Identität eines Community-Mitglieds.
/// </summary>
/// <remarks>
/// Die Domain-Identität trägt zunächst ausschließlich ihre interne Kennung. Namen,
/// Plattformkonten, Authentifizierung und weitere fachliche Eigenschaften gehören in
/// spätere, durch konkrete Anwendungsfälle begründete Erweiterungen.
/// </remarks>
public sealed class CommunityIdentity
{
    private CommunityIdentity(CommunityIdentityId id)
    {
        Id = id;
    }

    /// <summary>
    /// Liefert die unveränderliche interne Kennung dieser Community-Identität.
    /// </summary>
    public CommunityIdentityId Id { get; }

    /// <summary>
    /// Erstellt eine gültige Community-Identität mit der angegebenen internen Kennung.
    /// </summary>
    /// <param name="id">Die interne, nicht leere Community-Identity-ID.</param>
    /// <returns>Eine gültige Community-Identität.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="id"/> keine gültige Kennung enthält.</exception>
    public static CommunityIdentity Create(CommunityIdentityId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Community-Identität benötigt eine nicht leere Community-Identity-ID.",
                nameof(id));
        }

        return new CommunityIdentity(id);
    }
}
