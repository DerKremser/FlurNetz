using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>Listet externe Identitätsverknüpfungen einer internen Community-Identität.</summary>
public sealed class ListExternalIdentityMappings
{
    private readonly IExternalIdentityMappingStore store;

    /// <summary>Erstellt den List-Use-Case.</summary>
    public ListExternalIdentityMappings(IExternalIdentityMappingStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Lädt die deterministisch sortierte Mapping-Liste.</summary>
    public Task<IReadOnlyList<ExternalIdentityMapping>> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        return store.ListForCommunityIdentityAsync(validCommunityIdentityId, cancellationToken);
    }
}
