using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Contracts;

namespace FlurNetz.Modules.Integrations.Domain;

/// <summary>
/// Ordnet eine externe Provider-/Benutzerkennung genau einer internen Community-Identität zu.
/// </summary>
public sealed class ExternalIdentityMapping
{
    private ExternalIdentityMapping(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CommunityIdentityId communityIdentityId)
    {
        ProviderKey = providerKey;
        ExternalUserId = externalUserId;
        CommunityIdentityId = communityIdentityId;
    }

    /// <summary>Der stabile Provider-Key.</summary>
    public IntegrationProviderKey ProviderKey { get; }

    /// <summary>Die opaque externe Benutzerkennung.</summary>
    public ExternalUserId ExternalUserId { get; }

    /// <summary>Die zentrale interne FlurNetz-Identität.</summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>Erstellt ein validiertes Mapping.</summary>
    public static ExternalIdentityMapping Create(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CommunityIdentityId communityIdentityId)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        return new ExternalIdentityMapping(
            validProviderKey,
            validExternalUserId,
            validCommunityIdentityId);
    }

    /// <summary>
    /// Rekonstruiert ein Mapping aus bereits gespeicherten Werten und validiert sie erneut.
    /// </summary>
    public static ExternalIdentityMapping Rehydrate(
        string providerKey,
        string externalUserId,
        Guid communityIdentityId)
    {
        return Create(
            IntegrationProviderKey.Create(providerKey),
            ExternalUserId.Create(externalUserId),
            CommunityIdentityId.Create(communityIdentityId));
    }
}
