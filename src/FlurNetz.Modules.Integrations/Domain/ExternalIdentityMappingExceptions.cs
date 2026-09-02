using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Contracts;

namespace FlurNetz.Modules.Integrations.Domain;

/// <summary>Wird ausgelöst, wenn die Zielidentität beim Verknüpfen nicht existiert.</summary>
public sealed class CommunityIdentityNotFoundForExternalMappingException : Exception
{
    /// <summary>Erstellt den fachlichen Fehler für die unbekannte Zielidentität.</summary>
    public CommunityIdentityNotFoundForExternalMappingException(CommunityIdentityId communityIdentityId)
        : base($"Die Community-Identität '{communityIdentityId.Value}' wurde nicht gefunden.")
    {
        CommunityIdentityId = communityIdentityId;
    }

    /// <summary>Die nicht gefundene Zielidentität.</summary>
    public CommunityIdentityId CommunityIdentityId { get; }
}

/// <summary>
/// Wird ausgelöst, wenn eine externe Identität bereits einer anderen Community-Identität gehört.
/// </summary>
public sealed class ExternalIdentityMappingConflictException : Exception
{
    /// <summary>Erstellt den fachlichen Konfliktfehler.</summary>
    public ExternalIdentityMappingConflictException(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CommunityIdentityId existingCommunityIdentityId,
        CommunityIdentityId requestedCommunityIdentityId)
        : base($"Die externe Identität '{providerKey}/{externalUserId}' ist bereits einer anderen Community-Identität zugeordnet.")
    {
        ProviderKey = providerKey;
        ExternalUserId = externalUserId;
        ExistingCommunityIdentityId = existingCommunityIdentityId;
        RequestedCommunityIdentityId = requestedCommunityIdentityId;
    }

    /// <summary>Der Provider des Konflikts.</summary>
    public IntegrationProviderKey ProviderKey { get; }

    /// <summary>Die externe Kennung des Konflikts.</summary>
    public ExternalUserId ExternalUserId { get; }

    /// <summary>Die bereits verknüpfte interne Identität.</summary>
    public CommunityIdentityId ExistingCommunityIdentityId { get; }

    /// <summary>Die angeforderte interne Identität.</summary>
    public CommunityIdentityId RequestedCommunityIdentityId { get; }
}
