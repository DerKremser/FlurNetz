using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>
/// Interne Persistenzgrenze für External-Identity-Mappings.
/// </summary>
public interface IExternalIdentityMappingStore
{
    /// <summary>Verknüpft ein Mapping oder liefert dessen idempotenten/conflicthaften Zustand.</summary>
    Task<ExternalIdentityLinkResult> LinkAsync(
        ExternalIdentityMapping mapping,
        CancellationToken cancellationToken = default);

    /// <summary>Lädt ein Mapping über seine externe Identität.</summary>
    Task<ExternalIdentityMapping?> GetAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Listet alle Mappings einer Community-Identität deterministisch.</summary>
    Task<IReadOnlyList<ExternalIdentityMapping>> ListForCommunityIdentityAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);

    /// <summary>Entfernt ein Mapping und meldet, ob eine Zuordnung vorhanden war.</summary>
    Task<bool> UnlinkAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis des atomaren Link-Versuchs.</summary>
public sealed record ExternalIdentityLinkResult(
    ExternalIdentityLinkStatus Status,
    CommunityIdentityId? ExistingCommunityIdentityId = null);

/// <summary>Fachlicher Zustand eines Link-Versuchs.</summary>
public enum ExternalIdentityLinkStatus
{
    Linked = 0,
    AlreadyLinked = 1,
    CommunityIdentityNotFound = 2,
    Conflict = 3
}
