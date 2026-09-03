using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;
using System.Data.Common;

namespace FlurNetz.Modules.Identity.Tests;

public sealed class CreateCommunityIdentityTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesAndPersistsANewInternalIdentity()
    {
        var repository = new InMemoryCommunityIdentityRepository();
        var useCase = new CreateCommunityIdentity(repository);

        var id = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        var stored = Assert.Single(repository.Identities);
        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(id, stored.Id);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDistinctIdentitiesForSeparateExecutions()
    {
        var repository = new InMemoryCommunityIdentityRepository();
        var useCase = new CreateCommunityIdentity(repository);

        var first = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
        Assert.Equal(2, repository.Identities.Count);
    }

    private sealed class InMemoryCommunityIdentityRepository : ICommunityIdentityRepository
    {
        public List<CommunityIdentity> Identities { get; } = [];

        public Task AddAsync(CommunityIdentity identity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Identities.Add(identity);
            return Task.CompletedTask;
        }

        public Task AddAsync(
            CommunityIdentity identity,
            DbConnection connection,
            DbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The in-memory test repository does not provide ADO.NET persistence.");

        public Task<CommunityIdentity?> GetByIdAsync(
            CommunityIdentityId id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Identities.SingleOrDefault(identity => identity.Id == id));
        }
    }
}
