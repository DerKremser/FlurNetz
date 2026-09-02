using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Contracts;

namespace FlurNetz.Modules.Integrations.Application;

/// <summary>Führt eine read-only Auflösung externer Identitäten aus.</summary>
public sealed class ResolveExternalIdentity
{
    private readonly IExternalIdentityResolution resolution;

    /// <summary>Erstellt den Resolution-Use-Case.</summary>
    public ResolveExternalIdentity(IExternalIdentityResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        this.resolution = resolution;
    }

    /// <summary>Löst ein Mapping auf oder liefert <see langword="null"/>.</summary>
    public Task<CommunityIdentityId?> ExecuteAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);
        return resolution.ResolveAsync(validProviderKey, validExternalUserId, cancellationToken);
    }
}
