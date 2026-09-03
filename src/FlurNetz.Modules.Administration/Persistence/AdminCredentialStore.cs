using System.Data.Common;
using Dapper;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Persistence;

public sealed class AdminCredentialStore : IAdminCredentialStore
{
    private const string SelectByLoginSql = """
        SELECT community_identity_id AS CommunityIdentityId, login_name AS LoginName,
               password_hash AS PasswordHash, credential_version AS CredentialVersion,
               created_at_utc AS CreatedAtUtc, password_changed_at_utc AS PasswordChangedAtUtc
        FROM administration_credentials
        WHERE normalized_login_name = @NormalizedLoginName;
        """;
    private const string SelectByIdentitySql = """
        SELECT community_identity_id AS CommunityIdentityId, login_name AS LoginName,
               password_hash AS PasswordHash, credential_version AS CredentialVersion,
               created_at_utc AS CreatedAtUtc, password_changed_at_utc AS PasswordChangedAtUtc
        FROM administration_credentials
        WHERE community_identity_id = @CommunityIdentityId;
        """;
    private const string RoleSql = """
        SELECT EXISTS (
            SELECT 1 FROM administration_role_assignments
            WHERE community_identity_id = @CommunityIdentityId AND role_name = @RoleName);
        """;
    private const string InsertCredentialSql = """
        INSERT INTO administration_credentials
            (community_identity_id, login_name, normalized_login_name, password_hash,
             credential_version, created_at_utc, password_changed_at_utc)
        VALUES
            (@CommunityIdentityId, @LoginName, @NormalizedLoginName, @PasswordHash,
             @CredentialVersion, @CreatedAtUtc, @PasswordChangedAtUtc);
        """;
    private const string InsertRoleSql = """
        INSERT INTO administration_role_assignments
            (community_identity_id, role_name, created_at_utc)
        VALUES (@CommunityIdentityId, @RoleName, @CreatedAtUtc);
        """;
    private const string UpdatePasswordSql = """
        UPDATE administration_credentials
        SET password_hash = @PasswordHash,
            credential_version = @CredentialVersion,
            password_changed_at_utc = @PasswordChangedAtUtc
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    public AdminCredentialStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AdminCredential?> GetByLoginNameAsync(string normalizedLoginName, CancellationToken cancellationToken = default)
    {
        var normalized = AdminLoginName.Normalize(normalizedLoginName);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(
                new CommandDefinition(SelectByLoginSql, new { NormalizedLoginName = normalized }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row is null ? null : row.ToDomain();
    }

    public async Task<AdminCredential?> GetByLoginNameAsync(string normalizedLoginName, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var normalized = AdminLoginName.Normalize(normalizedLoginName);
        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(new CommandDefinition(
                SelectByLoginSql,
                new { NormalizedLoginName = normalized },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : row.ToDomain();
    }

    public async Task<AdminCredential?> GetByIdentityAsync(CommunityIdentityId identityId, CancellationToken cancellationToken = default)
    {
        var validId = CommunityIdentityId.Create(identityId.Value);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(
                new CommandDefinition(SelectByIdentitySql, new { CommunityIdentityId = validId.Value }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row is null ? null : row.ToDomain();
    }

    public async Task<AdminCredential?> GetByIdentityAsync(CommunityIdentityId identityId, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var validId = CommunityIdentityId.Create(identityId.Value);
        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(new CommandDefinition(
                SelectByIdentitySql,
                new { CommunityIdentityId = validId.Value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : row.ToDomain();
    }

    public async Task<bool> HasRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, CancellationToken cancellationToken = default)
    {
        var validId = CommunityIdentityId.Create(identityId.Value);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<bool>(new CommandDefinition(
                RoleSql,
                new { CommunityIdentityId = validId.Value, RoleName = roleName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public Task<bool> HasRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var validId = CommunityIdentityId.Create(identityId.Value);
        return connection.QuerySingleAsync<bool>(new CommandDefinition(
            RoleSql,
            new { CommunityIdentityId = validId.Value, RoleName = roleName },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public Task AddCredentialAsync(AdminCredential credential, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return connection.ExecuteAsync(new CommandDefinition(
            InsertCredentialSql,
            new
            {
                CommunityIdentityId = credential.CommunityIdentityId.Value,
                credential.LoginName,
                NormalizedLoginName = credential.NormalizedLoginName,
                PasswordHash = credential.PasswordHash,
                credential.CredentialVersion,
                credential.CreatedAtUtc,
                credential.PasswordChangedAtUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public Task AddRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return connection.ExecuteAsync(new CommandDefinition(
            InsertRoleSql,
            new { CommunityIdentityId = CommunityIdentityId.Create(identityId.Value).Value, RoleName = roleName, CreatedAtUtc = DateTimeOffset.UtcNow },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public async Task ChangePasswordAsync(AdminCredential credential, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var count = await connection.ExecuteAsync(new CommandDefinition(
                UpdatePasswordSql,
                new
                {
                    CommunityIdentityId = credential.CommunityIdentityId.Value,
                    PasswordHash = credential.PasswordHash,
                    credential.CredentialVersion,
                    credential.PasswordChangedAtUtc
                },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (count != 1)
        {
            throw new InvalidOperationException("Das Admin-Credential konnte nicht eindeutig aktualisiert werden.");
        }
    }

    private sealed class CredentialRow
    {
        public Guid CommunityIdentityId { get; set; }
        public string LoginName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public long CredentialVersion { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset PasswordChangedAtUtc { get; set; }

        public AdminCredential ToDomain() => AdminCredential.Rehydrate(
            FlurNetz.Modules.Identity.Contracts.CommunityIdentityId.Create(CommunityIdentityId),
            LoginName,
            PasswordHash,
            CredentialVersion,
            CreatedAtUtc,
            PasswordChangedAtUtc);
    }
}
