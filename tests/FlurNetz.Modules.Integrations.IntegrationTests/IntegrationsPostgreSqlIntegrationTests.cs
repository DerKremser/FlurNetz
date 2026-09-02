using Dapper;
using FlurNetz.Modules.Identity.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;
using FlurNetz.Modules.Integrations.Migrations;
using FlurNetz.Modules.Integrations.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Integrations.IntegrationTests;

/// <summary>Prüft Mapping, Resolution, Konflikte und Concurrency gegen echtes PostgreSQL.</summary>
public sealed class IntegrationsPostgreSqlIntegrationTests(IntegrationsPostgreSqlFixture database)
    : IClassFixture<IntegrationsPostgreSqlFixture>
{
    [Fact]
    public async Task MigrationIsIdempotentAndCreatesOnlyTheOwnedMappingTable()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await ResetAsync(factory);

        var source = new IntegrationsMigrationSource();
        var first = await new MigrationRunner(factory, source).RunAsync(TestToken);
        var second = await new MigrationRunner(factory, source).RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), first);
        Assert.Equal(new MigrationRunResult(0, 1), second);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var tableExists = await connection.QuerySingleAsync<bool>(
            new CommandDefinition(
                "SELECT to_regclass('public.integration_external_identity_mappings') IS NOT NULL;",
                cancellationToken: TestToken));
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = 'integration_external_identity_mappings'::regclass
                  AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var primaryKeyColumns = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT a.attname
                    FROM pg_index i
                    JOIN pg_attribute a
                      ON a.attrelid = i.indrelid
                     AND a.attnum = ANY(i.indkey)
                    WHERE i.indrelid = 'integration_external_identity_mappings'::regclass
                      AND i.indisprimary
                    ORDER BY array_position(i.indkey, a.attnum);
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Integrations' AND version = 1;
                """,
                cancellationToken: TestToken));

        Assert.True(tableExists);
        Assert.Equal(0, foreignKeyCount);
        Assert.Equal(["provider_key", "external_user_id"], primaryKeyColumns);
        Assert.Equal("Integrations", history.Owner);
        Assert.Equal(1, history.Version);
        Assert.Equal("CreateExternalIdentityMappings", history.Name);
        Assert.Equal(MigrationChecksum.Compute(source.GetMigrations().Single().Sql), history.Checksum);
    }

    [Fact]
    public async Task LinkResolveListAndUnlinkRoundTrip()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var identityId = await CreateIdentityAsync(factory);
        var store = CreateStore(factory);
        var link = new LinkExternalIdentity(store);
        var externalUserId = ExternalUserId.Create("opaque-twitch-user");

        var mapping = await link.ExecuteAsync(
            IntegrationProviderKey.Twitch,
            externalUserId,
            identityId,
            TestToken);
        var resolved = await store.ResolveAsync(
            IntegrationProviderKey.Twitch,
            externalUserId,
            TestToken);
        var listed = await new ListExternalIdentityMappings(store).ExecuteAsync(identityId, TestToken);

        Assert.Equal(identityId, mapping.CommunityIdentityId);
        Assert.Equal(identityId, resolved);
        Assert.Single(listed);
        Assert.Equal(mapping.ProviderKey, listed[0].ProviderKey);
        Assert.True(await new UnlinkExternalIdentity(store).ExecuteAsync(
            IntegrationProviderKey.Twitch,
            externalUserId,
            TestToken));
        Assert.Null(await store.GetAsync(IntegrationProviderKey.Twitch, externalUserId, TestToken));
        Assert.False(await new UnlinkExternalIdentity(store).ExecuteAsync(
            IntegrationProviderKey.Twitch,
            externalUserId,
            TestToken));
    }

    [Fact]
    public async Task LinkRequiresAnExistingIdentityAndRejectsReassignment()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var store = CreateStore(factory);
        var externalUserId = ExternalUserId.Create("123");
        var missingIdentity = CommunityIdentityId.New();

        await Assert.ThrowsAsync<CommunityIdentityNotFoundForExternalMappingException>(
            () => new LinkExternalIdentity(store).ExecuteAsync(
                IntegrationProviderKey.Twitch,
                externalUserId,
                missingIdentity,
                TestToken));

        var existingIdentity = await CreateIdentityAsync(factory);
        await new LinkExternalIdentity(store).ExecuteAsync(
            IntegrationProviderKey.Twitch,
            externalUserId,
            existingIdentity,
            TestToken);

        var reassignment = await CreateIdentityAsync(factory);
        var exception = await Assert.ThrowsAsync<ExternalIdentityMappingConflictException>(
            () => new LinkExternalIdentity(store).ExecuteAsync(
                IntegrationProviderKey.Twitch,
                externalUserId,
                reassignment,
                TestToken));

        Assert.Equal(existingIdentity, exception.ExistingCommunityIdentityId);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var count = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM integration_external_identity_mappings;",
                cancellationToken: TestToken));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task LinkRollsBackWhenTheDatabaseRejectsTheInsert()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var identityId = await CreateIdentityAsync(factory);

        await using (var connection = await factory.OpenConnectionAsync(TestToken))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    CREATE OR REPLACE FUNCTION flurnetz_test_reject_integration_mapping()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $$
                    BEGIN
                        RAISE EXCEPTION 'integration mapping insert rejected for rollback test';
                    END;
                    $$;
                    CREATE TRIGGER flurnetz_test_reject_integration_mapping_trigger
                    BEFORE INSERT ON integration_external_identity_mappings
                    FOR EACH ROW
                    EXECUTE FUNCTION flurnetz_test_reject_integration_mapping();
                    """,
                    cancellationToken: TestToken));
        }

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => CreateStore(factory).LinkAsync(
                ExternalIdentityMapping.Create(
                    IntegrationProviderKey.Twitch,
                    ExternalUserId.Create("rollback"),
                    identityId),
                TestToken));
        }
        finally
        {
            await using var connection = await factory.OpenConnectionAsync(TestToken);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    DROP TRIGGER IF EXISTS flurnetz_test_reject_integration_mapping_trigger
                        ON integration_external_identity_mappings;
                    DROP FUNCTION IF EXISTS flurnetz_test_reject_integration_mapping();
                    """,
                    cancellationToken: TestToken));
        }

        await using var verificationConnection = await factory.OpenConnectionAsync(TestToken);
        var mappingCount = await verificationConnection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM integration_external_identity_mappings;",
                cancellationToken: TestToken));
        Assert.Equal(0, mappingCount);
    }

    [Fact]
    public async Task ConcurrentLinksKeepOneMappingForDifferentIdentities()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var firstIdentity = await CreateIdentityAsync(factory);
        var secondIdentity = await CreateIdentityAsync(factory);
        var externalUserId = ExternalUserId.Create("concurrent");

        var firstTask = LinkWithNewStoreAsync(factory, firstIdentity, externalUserId);
        var secondTask = LinkWithNewStoreAsync(factory, secondIdentity, externalUserId);
        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, outcomes.Count(outcome => outcome is LinkOutcome.Linked));
        Assert.Equal(1, outcomes.Count(outcome => outcome is LinkOutcome.Conflict));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var mappings = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT community_identity_id FROM integration_external_identity_mappings WHERE provider_key = 'twitch' AND external_user_id = 'concurrent';",
                cancellationToken: TestToken));
        Assert.Single(mappings);
    }

    [Fact]
    public async Task ConcurrentIdenticalLinksAreIdempotent()
    {
        SkipIfUnavailable();
        await using var factory = CreateFactory();
        await PrepareAsync(factory);
        var identityId = await CreateIdentityAsync(factory);
        var externalUserId = ExternalUserId.Create("same");

        var outcomes = await Task.WhenAll(
            LinkWithNewStoreAsync(factory, identityId, externalUserId),
            LinkWithNewStoreAsync(factory, identityId, externalUserId));

        Assert.All(outcomes, outcome => Assert.Contains(
            outcome,
            new[] { LinkOutcome.Linked, LinkOutcome.AlreadyLinked }));

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var count = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM integration_external_identity_mappings WHERE provider_key = 'twitch' AND external_user_id = 'same';",
                cancellationToken: TestToken));
        Assert.Equal(1, count);
    }

    private async Task<LinkOutcome> LinkWithNewStoreAsync(
        PostgreSqlConnectionFactory factory,
        CommunityIdentityId identityId,
        ExternalUserId externalUserId)
    {
        var result = await CreateStore(factory).LinkAsync(
            ExternalIdentityMapping.Create(
                IntegrationProviderKey.Twitch,
                externalUserId,
                identityId),
            TestToken);
        return result.Status switch
        {
            ExternalIdentityLinkStatus.Linked => LinkOutcome.Linked,
            ExternalIdentityLinkStatus.AlreadyLinked => LinkOutcome.AlreadyLinked,
            ExternalIdentityLinkStatus.Conflict => LinkOutcome.Conflict,
            _ => throw new InvalidOperationException($"Unexpected link result {result.Status}.")
        };
    }

    private async Task<CommunityIdentityId> CreateIdentityAsync(PostgreSqlConnectionFactory factory) =>
        await new CreateCommunityIdentity(new CommunityIdentityRepository(factory)).ExecuteAsync(TestToken);

    private PostgreSqlExternalIdentityMappingStore CreateStore(PostgreSqlConnectionFactory factory) =>
        new(factory, new CommunityIdentityExistence());

    private async Task PrepareAsync(PostgreSqlConnectionFactory factory)
    {
        await ResetAsync(factory);
        await new MigrationRunner(
                factory,
                new IMigrationSource[]
                {
                    new IdentityMigrationSource(),
                    new IntegrationsMigrationSource()
                })
            .RunAsync(TestToken);
    }

    private static async Task ResetAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new IntegrationsMigrationSource()).RunAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS integration_external_identity_mappings;
                DROP TABLE IF EXISTS community_identities;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner IN ('Integrations', 'Identity');
                """,
                cancellationToken: TestToken));
    }

    private PostgreSqlConnectionFactory CreateFactory() =>
        new(new PostgreSqlOptions(database.ConnectionString));

    private void SkipIfUnavailable() =>
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private enum LinkOutcome
    {
        Linked,
        AlreadyLinked,
        Conflict
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public int Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
