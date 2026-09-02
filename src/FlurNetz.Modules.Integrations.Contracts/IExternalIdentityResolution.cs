using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Integrations.Contracts;

/// <summary>
/// Caller-neutrale Fähigkeit zur read-only Auflösung einer externen Identität.
/// </summary>
/// <remarks>
/// Der Contract veröffentlicht weder Repository-, Datenbank- noch Domain-Details. Eine
/// unbekannte externe Identität wird als <see langword="null"/> zurückgegeben; es wird
/// keine Community-Identität automatisch erzeugt.
/// </remarks>
public interface IExternalIdentityResolution
{
    /// <summary>
    /// Löst eine externe Provider-/User-Kombination auf eine interne Community-Identität auf.
    /// </summary>
    Task<CommunityIdentityId?> ResolveAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default);
}
