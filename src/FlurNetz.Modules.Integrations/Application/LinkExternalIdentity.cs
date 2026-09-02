using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>Verknüpft eine externe Identität ohne stilles Reassignment.</summary>
public sealed class LinkExternalIdentity
{
    private readonly IExternalIdentityMappingStore store;

    /// <summary>Erstellt den Link-Use-Case.</summary>
    public LinkExternalIdentity(IExternalIdentityMappingStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Führt einen validierten und bei Wiederholung idempotenten Link-Versuch aus.
    /// </summary>
    public async Task<ExternalIdentityMapping> ExecuteAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var mapping = ExternalIdentityMapping.Create(providerKey, externalUserId, communityIdentityId);
        var result = await store.LinkAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            ExternalIdentityLinkStatus.Linked or ExternalIdentityLinkStatus.AlreadyLinked => mapping,
            ExternalIdentityLinkStatus.CommunityIdentityNotFound => throw new CommunityIdentityNotFoundForExternalMappingException(communityIdentityId),
            ExternalIdentityLinkStatus.Conflict => throw new ExternalIdentityMappingConflictException(
                mapping.ProviderKey,
                mapping.ExternalUserId,
                result.ExistingCommunityIdentityId
                    ?? throw new InvalidOperationException("A mapping conflict must provide the existing community identity."),
                mapping.CommunityIdentityId),
            _ => throw new InvalidOperationException("Unknown external identity link result.")
        };
    }
}
