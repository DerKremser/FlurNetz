using FlurNetz.Modules.Integrations.Contracts;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>Entfernt bewusst genau eine externe Identitätsverknüpfung.</summary>
public sealed class UnlinkExternalIdentity
{
    private readonly IExternalIdentityMappingStore store;

    /// <summary>Erstellt den Unlink-Use-Case.</summary>
    public UnlinkExternalIdentity(IExternalIdentityMappingStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Entfernt das Mapping. Ein unbekanntes Mapping liefert <see langword="false"/> und
    /// verändert keine anderen Integrations- oder Identity-Daten.
    /// </summary>
    public Task<bool> ExecuteAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);
        return store.UnlinkAsync(validProviderKey, validExternalUserId, cancellationToken);
    }
}
