using Dapper;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Migrations;
using FlurNetz.Modules.Administration.Persistence;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Migrations;
using FlurNetz.Modules.Identity.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Administration.IntegrationTests;

public sealed class AdministrationPostgreSqlIntegrationTests(AdministrationPostgreSqlFixture database)
    : IClassFixture<AdministrationPostgreSqlFixture>
{
    [Fact]
    public async Task AdministrationMigrationIsIdempotentAndCreatesOnlyOwnedTables()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        var first = await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        var second = await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        Assert.Equal(new MigrationRunResult(2, 0), first);
        Assert.Equal(new MigrationRunResult(0, 2), second);

        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        var tables = (await connection.QueryAsync<string>(new CommandDefinition("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'administration_%' ORDER BY table_name;", cancellationToken: cancellationToken))).ToArray();
        Assert.Equal(["administration_audit_entries", "administration_credentials", "administration_operations", "administration_role_assignments"], tables);
        var foreignKeys = await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM information_schema.table_constraints WHERE table_schema = 'public' AND table_name LIKE 'administration_%' AND constraint_type = 'FOREIGN KEY';", cancellationToken: cancellationToken));
        Assert.Equal(0, foreignKeys);
    }

    [Fact]
    public async Task BootstrapIsCreateIfMissingAndRecoveryRequestIsIdempotent()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        var identityId = CommunityIdentityId.New();
        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO community_identities (id) VALUES (@Id);", new { Id = identityId.Value }, cancellationToken: cancellationToken));
        }

        var credentialStore = new AdminCredentialStore(factory);
        var passwordHasher = new AdminPasswordHasher();
        var bootstrapper = new AdminBootstrapper(factory, new CommunityIdentityExistence(), credentialStore, passwordHasher);
        var configuration = new AdminBootstrapConfiguration(identityId, "Operator", "sentinel initial password");
        Assert.True(await bootstrapper.BootstrapAsync(configuration, cancellationToken));
        Assert.False(await bootstrapper.BootstrapAsync(configuration, cancellationToken));

        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            var row = await connection.QuerySingleAsync<(string Hash, long Version)>(new CommandDefinition("SELECT password_hash AS Hash, credential_version AS Version FROM administration_credentials WHERE community_identity_id = @Id;", new { Id = identityId.Value }, cancellationToken: cancellationToken));
            Assert.DoesNotContain("sentinel initial password", row.Hash, StringComparison.Ordinal);
            Assert.Equal(1, row.Version);
        }

        var recovery = new AdminCredentialRecovery(factory, credentialStore, new AdminOperationStore(factory), passwordHasher, new AdminAuditStore(factory));
        var requestId = Guid.NewGuid();
        Assert.True(await recovery.RecoverAsync(identityId, "sentinel recovered password", requestId, cancellationToken));
        Assert.False(await recovery.RecoverAsync(identityId, "sentinel recovered password", requestId, cancellationToken));
        var authentication = new AdminAuthenticationService(credentialStore, passwordHasher);
        Assert.False((await authentication.AuthenticateAsync("operator", "sentinel initial password", cancellationToken)).Succeeded);
        Assert.True((await authentication.AuthenticateAsync("operator", "sentinel recovered password", cancellationToken)).Succeeded);
    }

    [Fact]
    public async Task AdminMutationIsAtomicAndParallelSameRequestIdHasOneEffectAndOneAudit()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        var identityId = CommunityIdentityId.New();
        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO community_identities (id) VALUES (@Id); DROP TABLE IF EXISTS test_admin_effects; CREATE TABLE test_admin_effects (id integer PRIMARY KEY);", new { Id = identityId.Value }, cancellationToken: cancellationToken));
        }

        var operationStore = new AdminOperationStore(factory);
        var auditStore = new AdminAuditStore(factory);
        var coordinator = new AdminMutationCoordinator(factory, operationStore, auditStore);
        var requestId = Guid.NewGuid();
        var command = new AdminMutationCommand(
            requestId,
            identityId,
            "Test.AtomicMutation",
            "TestEffect",
            "one",
            AdminRequestFingerprint.Compute(("effect", "one")),
            "test-correlation",
            DateTimeOffset.UtcNow);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => coordinator.ExecuteAsync(
            command,
            async (connection, transaction, token) =>
            {
                await connection.ExecuteAsync(new CommandDefinition("INSERT INTO test_admin_effects (id) VALUES (1);", transaction: transaction, cancellationToken: token));
            },
            () => new AdminAuditEntry(
                Guid.NewGuid(), identityId, "operator", "Test.AtomicMutation", "TestEffect", "one", null,
                AdminRiskLevel.Medium, null, AdminAuditOutcome.Succeeded, DateTimeOffset.UtcNow,
                "test-correlation", requestId, null,
                new Dictionary<string, string?> { ["Changed"] = "true" }, new Dictionary<string, string?>()),
            cancellationToken)));

        Assert.Equal(1, results.Count(result => !result.AlreadyCompleted));
        Assert.Equal(19, results.Count(result => result.AlreadyCompleted));
        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM test_admin_effects;", cancellationToken: cancellationToken)));
            Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_audit_entries WHERE request_id = @RequestId;", new { RequestId = requestId }, cancellationToken: cancellationToken)));
            Assert.Equal("Succeeded", await connection.QuerySingleAsync<string>(new CommandDefinition("SELECT mutation_status FROM administration_operations WHERE request_id = @RequestId;", new { RequestId = requestId }, cancellationToken: cancellationToken)));
        }
    }

    [Fact]
    public async Task AdminMutationRollsBackOwnerAndReservationWhenAuditFactoryFails()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        var identityId = CommunityIdentityId.New();
        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO community_identities (id) VALUES (@Id); DROP TABLE IF EXISTS test_admin_effects; CREATE TABLE test_admin_effects (id integer PRIMARY KEY);", new { Id = identityId.Value }, cancellationToken: cancellationToken));
        }

        var coordinator = new AdminMutationCoordinator(factory, new AdminOperationStore(factory), new AdminAuditStore(factory));
        var requestId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            new AdminMutationCommand(requestId, identityId, "Test.Rollback", "TestEffect", "one", new string('a', 64), "correlation", DateTimeOffset.UtcNow),
            async (connection, transaction, token) => await connection.ExecuteAsync(new CommandDefinition("INSERT INTO test_admin_effects (id) VALUES (1);", transaction: transaction, cancellationToken: token)),
            () => throw new InvalidOperationException("intentional audit failure"),
            cancellationToken));

        await using var check = await factory.OpenConnectionAsync(cancellationToken);
        Assert.Equal(0, await check.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM test_admin_effects;", cancellationToken: cancellationToken)));
        Assert.Equal(0, await check.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_operations WHERE request_id = @RequestId;", new { RequestId = requestId }, cancellationToken: cancellationToken)));
        Assert.Equal(0, await check.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_audit_entries WHERE request_id = @RequestId;", new { RequestId = requestId }, cancellationToken: cancellationToken)));
    }

    private PostgreSqlConnectionFactory CreateFactory() => new(new PostgreSqlOptions(database.ConnectionString));
    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
}
