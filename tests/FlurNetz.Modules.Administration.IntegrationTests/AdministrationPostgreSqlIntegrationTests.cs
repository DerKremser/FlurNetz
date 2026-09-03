using Dapper;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Migrations;
using FlurNetz.Modules.Administration.Persistence;
using FlurNetz.Modules.Identity.Application;
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
        Assert.Equal(["administration_audit_entries", "administration_credentials", "administration_operations", "administration_role_assignments", "administration_setup_state"], tables);
        var foreignKeys = await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM information_schema.table_constraints WHERE table_schema = 'public' AND table_name LIKE 'administration_%' AND constraint_type = 'FOREIGN KEY';", cancellationToken: cancellationToken));
        Assert.Equal(0, foreignKeys);
    }

    [Fact]
    public async Task FirstRunSetupIsGatedSingleUseAndRecoveryRequestIsIdempotent()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);
        var credentialStore = new AdminCredentialStore(factory);
        var passwordHasher = new AdminPasswordHasher();
        var setup = new AdminFirstRunSetup(
            factory,
            new CreateCommunityIdentity(new CommunityIdentityRepository(factory)),
            credentialStore,
            passwordHasher,
            new AdminSetupGateConfiguration("sentinel setup gate"));
        await Assert.ThrowsAsync<AdminSetupGateException>(() => setup.CreateFirstAdministratorAsync(
            "operator@example.com", "sentinel initial password", "sentinel initial password", "wrong gate", cancellationToken));
        var credential = await setup.CreateFirstAdministratorAsync(
            "Operator@Example.com", "sentinel initial password", "sentinel initial password", "sentinel setup gate", cancellationToken);
        Assert.Equal("Operator@Example.com", credential.Email);
        Assert.False(await setup.IsAvailableAsync(cancellationToken));
        await Assert.ThrowsAsync<AdminSetupClosedException>(() => setup.CreateFirstAdministratorAsync(
            "second@example.com", "sentinel second password", "sentinel second password", "sentinel setup gate", cancellationToken));

        await using (var connection = await factory.OpenConnectionAsync(cancellationToken))
        {
            var row = await connection.QuerySingleAsync<(Guid IdentityId, string Hash, string NormalizedEmail, long Version)>(new CommandDefinition("SELECT community_identity_id AS IdentityId, password_hash AS Hash, normalized_email AS NormalizedEmail, credential_version AS Version FROM administration_credentials;", cancellationToken: cancellationToken));
            Assert.DoesNotContain("sentinel initial password", row.Hash, StringComparison.Ordinal);
            Assert.Equal("OPERATOR@EXAMPLE.COM", row.NormalizedEmail);
            Assert.Equal(1, row.Version);
            Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM community_identities;", cancellationToken: cancellationToken)));
            Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_role_assignments;", cancellationToken: cancellationToken)));
            var completedAt = await connection.QuerySingleOrDefaultAsync<DateTime?>(new CommandDefinition("SELECT completed_at_utc FROM administration_setup_state WHERE id = 1;", cancellationToken: cancellationToken));
            Assert.True(completedAt.HasValue);
            Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_audit_entries;", cancellationToken: cancellationToken)));
            Assert.Equal(0, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_operations;", cancellationToken: cancellationToken)));
        }

        var identityId = credential.CommunityIdentityId;
        var recovery = new AdminCredentialRecovery(factory, credentialStore, new AdminOperationStore(factory), passwordHasher, new AdminAuditStore(factory));
        var requestId = Guid.NewGuid();
        Assert.True(await recovery.RecoverAsync(identityId, "sentinel recovered password", requestId, cancellationToken));
        Assert.False(await recovery.RecoverAsync(identityId, "sentinel recovered password", requestId, cancellationToken));
        var authentication = new AdminAuthenticationService(credentialStore, passwordHasher);
        Assert.False((await authentication.AuthenticateAsync("operator@example.com", "sentinel initial password", cancellationToken)).Succeeded);
        Assert.True((await authentication.AuthenticateAsync("operator@example.com", "sentinel recovered password", cancellationToken)).Succeeded);
    }

    [Fact]
    public async Task ParallelFirstRunSetupCreatesExactlyOneAdministrator()
    {
        SkipIfUnavailable();
        var cancellationToken = TestContext.Current.CancellationToken;
        await database.ResetAsync(cancellationToken);
        await using var factory = CreateFactory();
        await new MigrationRunner(factory, [new IdentityMigrationSource(), new AdministrationMigrationSource()]).RunAsync(cancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var setup = new AdminFirstRunSetup(
                factory,
                new CreateCommunityIdentity(new CommunityIdentityRepository(factory)),
                new AdminCredentialStore(factory),
                new AdminPasswordHasher(),
                new AdminSetupGateConfiguration("parallel setup gate"));
            try
            {
                await setup.CreateFirstAdministratorAsync(
                    "parallel@example.com",
                    "sentinel parallel password",
                    "sentinel parallel password",
                    "parallel setup gate",
                    cancellationToken);
                return true;
            }
            catch (AdminSetupClosedException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(result => result));
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM community_identities;", cancellationToken: cancellationToken)));
        Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_credentials;", cancellationToken: cancellationToken)));
        Assert.Equal(1, await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT count(*) FROM administration_role_assignments;", cancellationToken: cancellationToken)));
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
