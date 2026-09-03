namespace FlurNetz.Modules.Identity.Contracts;

public sealed record CommunityIdentitySummary(CommunityIdentityId CommunityIdentityId);

public sealed record CommunityIdentityPage(
    IReadOnlyList<CommunityIdentitySummary> Items,
    CommunityIdentityId? NextCursor);

/// <summary>Read-only, keyset-paginierte Identitätsfähigkeit für administrative Reads.</summary>
public interface ICommunityIdentityRead
{
    Task<CommunityIdentitySummary?> GetAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default);

    Task<CommunityIdentityPage> ListAsync(
        CommunityIdentityId? after,
        int pageSize,
        CancellationToken cancellationToken = default);
}
