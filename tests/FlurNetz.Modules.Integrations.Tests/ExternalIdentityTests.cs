using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Modules.Integrations.Tests;

/// <summary>Prüft die stabilen externen Identifier und das Mapping-Domainmodell.</summary>
public sealed class ExternalIdentityDomainTests
{
    [Fact]
    public void ProviderKeyIsCanonicalAndOrdinal()
    {
        var key = IntegrationProviderKey.Create(" Twitch ");

        Assert.Equal("twitch", key.Value);
        Assert.Equal(IntegrationProviderKey.Twitch, key);
        Assert.Equal(key, IntegrationProviderKey.Create("TWITCH"));
        Assert.Throws<ArgumentException>(() => IntegrationProviderKey.Create("twitch/provider"));
        Assert.Throws<ArgumentException>(() => IntegrationProviderKey.Create("-twitch"));
        Assert.Throws<ArgumentException>(() => IntegrationProviderKey.Create("twitch-"));
    }

    [Fact]
    public void ExternalUserIdRemainsOpaqueAndRejectsUnsafeShape()
    {
        var userId = ExternalUserId.Create("00123");

        Assert.Equal("00123", userId.Value);
        Assert.NotEqual(ExternalUserId.Create("123"), userId);
        Assert.Throws<ArgumentException>(() => ExternalUserId.Create(string.Empty));
        Assert.Throws<ArgumentException>(() => ExternalUserId.Create(" 123"));
        Assert.Throws<ArgumentException>(() => ExternalUserId.Create("123 "));
        Assert.Throws<ArgumentException>(() => ExternalUserId.Create("123\0"));
    }

    [Fact]
    public void MappingValidatesBothExternalAndInternalIdentity()
    {
        var communityIdentityId = CommunityIdentityId.New();
        var mapping = ExternalIdentityMapping.Create(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("123456789"),
            communityIdentityId);

        Assert.Equal(communityIdentityId, mapping.CommunityIdentityId);
        Assert.Throws<ArgumentException>(() => ExternalIdentityMapping.Create(
            default,
            ExternalUserId.Create("123"),
            communityIdentityId));
        Assert.Throws<ArgumentException>(() => ExternalIdentityMapping.Create(
            IntegrationProviderKey.Twitch,
            default,
            communityIdentityId));
        Assert.Throws<ArgumentException>(() => ExternalIdentityMapping.Create(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("123"),
            default));
    }
}

/// <summary>Prüft die Application-Semantik unabhängig von PostgreSQL.</summary>
public sealed class ExternalIdentityApplicationTests
{
    [Fact]
    public async Task IdenticalLinkIsIdempotent()
    {
        var store = new FakeExternalIdentityMappingStore
        {
            LinkResult = new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.AlreadyLinked)
        };
        var mapping = await new LinkExternalIdentity(store).ExecuteAsync(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("123"),
            CommunityIdentityId.New(),
            TestToken);

        Assert.Equal("123", mapping.ExternalUserId.Value);
        Assert.Single(store.LinkedMappings);
    }

    [Fact]
    public async Task LinkToUnknownCommunityIdentityIsRejected()
    {
        var store = new FakeExternalIdentityMappingStore
        {
            LinkResult = new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.CommunityIdentityNotFound)
        };
        var communityIdentityId = CommunityIdentityId.New();

        var exception = await Assert.ThrowsAsync<CommunityIdentityNotFoundForExternalMappingException>(
            () => new LinkExternalIdentity(store).ExecuteAsync(
                IntegrationProviderKey.Twitch,
                ExternalUserId.Create("123"),
                communityIdentityId,
                TestToken));

        Assert.Equal(communityIdentityId, exception.CommunityIdentityId);
        Assert.Empty(store.LinkedMappings);
    }

    [Fact]
    public async Task ReassignmentIsRejectedAsConflict()
    {
        var existingIdentity = CommunityIdentityId.New();
        var requestedIdentity = CommunityIdentityId.New();
        var store = new FakeExternalIdentityMappingStore
        {
            LinkResult = new ExternalIdentityLinkResult(
                ExternalIdentityLinkStatus.Conflict,
                existingIdentity)
        };

        var exception = await Assert.ThrowsAsync<ExternalIdentityMappingConflictException>(
            () => new LinkExternalIdentity(store).ExecuteAsync(
                IntegrationProviderKey.Twitch,
                ExternalUserId.Create("123"),
                requestedIdentity,
                TestToken));

        Assert.Equal(existingIdentity, exception.ExistingCommunityIdentityId);
        Assert.Equal(requestedIdentity, exception.RequestedCommunityIdentityId);
    }

    [Fact]
    public async Task UnlinkUnknownMappingHasExplicitFalseSemantics()
    {
        var store = new FakeExternalIdentityMappingStore { UnlinkResult = false };

        var removed = await new UnlinkExternalIdentity(store).ExecuteAsync(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("missing"),
            TestToken);

        Assert.False(removed);
    }

    [Fact]
    public async Task ResolutionReturnsMappedIdentityOrNull()
    {
        var identity = CommunityIdentityId.New();
        var store = new FakeExternalIdentityMappingStore
        {
            Resolution = identity
        };
        var useCase = new ResolveExternalIdentity(store);

        Assert.Equal(identity, await useCase.ExecuteAsync(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("123"),
            TestToken));

        store.Resolution = null;
        Assert.Null(await useCase.ExecuteAsync(
            IntegrationProviderKey.Twitch,
            ExternalUserId.Create("missing"),
            TestToken));
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class FakeExternalIdentityMappingStore : IExternalIdentityMappingStore, IExternalIdentityResolution
    {
        public ExternalIdentityLinkResult LinkResult { get; init; } =
            new(ExternalIdentityLinkStatus.Linked);

        public bool UnlinkResult { get; init; } = true;

        public CommunityIdentityId? Resolution { get; set; }

        public List<ExternalIdentityMapping> LinkedMappings { get; } = [];

        public Task<ExternalIdentityLinkResult> LinkAsync(
            ExternalIdentityMapping mapping,
            CancellationToken cancellationToken = default)
        {
            if (LinkResult.Status is ExternalIdentityLinkStatus.Linked or ExternalIdentityLinkStatus.AlreadyLinked)
            {
                LinkedMappings.Add(mapping);
            }

            return Task.FromResult(LinkResult);
        }

        public Task<ExternalIdentityMapping?> GetAsync(
            IntegrationProviderKey providerKey,
            ExternalUserId externalUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ExternalIdentityMapping?>(null);

        public Task<IReadOnlyList<ExternalIdentityMapping>> ListForCommunityIdentityAsync(
            CommunityIdentityId communityIdentityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalIdentityMapping>>([]);

        public Task<bool> UnlinkAsync(
            IntegrationProviderKey providerKey,
            ExternalUserId externalUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UnlinkResult);

        public Task<CommunityIdentityId?> ResolveAsync(
            IntegrationProviderKey providerKey,
            ExternalUserId externalUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Resolution);
    }
}
