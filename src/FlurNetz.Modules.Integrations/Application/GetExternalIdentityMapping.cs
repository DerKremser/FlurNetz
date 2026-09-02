using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>Lädt eine einzelne externe Identitätsverknüpfung.</summary>
public sealed class GetExternalIdentityMapping
{
    private readonly IExternalIdentityMappingStore store;

    /// <summary>Erstellt den Get-Use-Case.</summary>
    public GetExternalIdentityMapping(IExternalIdentityMappingStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Lädt das Mapping oder liefert <see langword="null"/>.</summary>
    public Task<ExternalIdentityMapping?> ExecuteAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);
        return store.GetAsync(validProviderKey, validExternalUserId, cancellationToken);
    }
}
