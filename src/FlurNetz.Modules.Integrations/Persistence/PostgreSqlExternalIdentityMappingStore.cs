using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Integrations.Persistence;

/// <summary>
/// Persistiert externe Identitätsverknüpfungen mit gezieltem Dapper-SQL in PostgreSQL.
/// </summary>
/// <remarks>
/// Der Link-Pfad prüft die interne Identity über deren öffentliche Capability in derselben
/// Transaktion und verlässt sich für die externe Eindeutigkeit zusätzlich auf den Primary Key.
/// </remarks>
public sealed class PostgreSqlExternalIdentityMappingStore :
    IExternalIdentityMappingStore,
    IExternalIdentityResolution
{
    private const string InsertSql = """
        INSERT INTO integration_external_identity_mappings
            (provider_key, external_user_id, community_identity_id)
        VALUES
            (@ProviderKey, @ExternalUserId, @CommunityIdentityId)
        ON CONFLICT (provider_key, external_user_id) DO NOTHING
        RETURNING community_identity_id;
        """;

    private const string SelectByExternalIdentitySql = """
        SELECT provider_key AS ProviderKey,
               external_user_id AS ExternalUserId,
               community_identity_id AS CommunityIdentityId
        FROM integration_external_identity_mappings
        WHERE provider_key = @ProviderKey
          AND external_user_id = @ExternalUserId;
        """;

    private const string SelectForCommunityIdentitySql = """
        SELECT provider_key AS ProviderKey,
               external_user_id AS ExternalUserId,
               community_identity_id AS CommunityIdentityId
        FROM integration_external_identity_mappings
        WHERE community_identity_id = @CommunityIdentityId
        ORDER BY provider_key ASC, external_user_id ASC;
        """;

    private const string SelectExistingCommunityIdentitySql = """
        SELECT community_identity_id
        FROM integration_external_identity_mappings
        WHERE provider_key = @ProviderKey
          AND external_user_id = @ExternalUserId;
        """;

    private const string DeleteSql = """
        DELETE FROM integration_external_identity_mappings
        WHERE provider_key = @ProviderKey
          AND external_user_id = @ExternalUserId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly ICommunityIdentityExistence identityExistence;

    /// <summary>Erstellt den PostgreSQL-Mappingstore.</summary>
    public PostgreSqlExternalIdentityMappingStore(
        IPostgreSqlConnectionFactory connectionFactory,
        ICommunityIdentityExistence identityExistence)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(identityExistence);
        this.connectionFactory = connectionFactory;
        this.identityExistence = identityExistence;
    }

    /// <inheritdoc />
    public async Task<ExternalIdentityLinkResult> LinkAsync(
        ExternalIdentityMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var exists = await identityExistence.ExistsAsync(
                    mapping.CommunityIdentityId,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.CommunityIdentityNotFound);
            }

            var insertedIdentityId = await transaction.Connection.QuerySingleOrDefaultAsync<Guid?>(
                    new CommandDefinition(
                        InsertSql,
                        new
                        {
                            ProviderKey = mapping.ProviderKey.Value,
                            ExternalUserId = mapping.ExternalUserId.Value,
                            CommunityIdentityId = mapping.CommunityIdentityId.Value
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (insertedIdentityId is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.Linked);
            }

            var existingIdentityId = await transaction.Connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        SelectExistingCommunityIdentitySql,
                        new
                        {
                            ProviderKey = mapping.ProviderKey.Value,
                            ExternalUserId = mapping.ExternalUserId.Value
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            var existing = CommunityIdentityId.Create(existingIdentityId);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return existing == mapping.CommunityIdentityId
                ? new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.AlreadyLinked, existing)
                : new ExternalIdentityLinkResult(ExternalIdentityLinkStatus.Conflict, existing);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExternalIdentityMapping?> GetAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<ExternalIdentityMappingRow>(
                new CommandDefinition(
                    SelectByExternalIdentitySql,
                    new
                    {
                        ProviderKey = validProviderKey.Value,
                        ExternalUserId = validExternalUserId.Value
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : row.ToDomain();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalIdentityMapping>> ListForCommunityIdentityAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<ExternalIdentityMappingRow>(
                new CommandDefinition(
                    SelectForCommunityIdentitySql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(row => row.ToDomain()).ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> UnlinkAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var validProviderKey = IntegrationProviderKey.Create(providerKey.Value);
        var validExternalUserId = ExternalUserId.Create(externalUserId.Value);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var affectedRows = await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        DeleteSql,
                        new
                        {
                            ProviderKey = validProviderKey.Value,
                            ExternalUserId = validExternalUserId.Value
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affectedRows == 1;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommunityIdentityId?> ResolveAsync(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId,
        CancellationToken cancellationToken = default)
    {
        var mapping = await GetAsync(providerKey, externalUserId, cancellationToken).ConfigureAwait(false);
        return mapping?.CommunityIdentityId;
    }

    private sealed class ExternalIdentityMappingRow
    {
        public string ProviderKey { get; set; } = string.Empty;

        public string ExternalUserId { get; set; } = string.Empty;

        public Guid CommunityIdentityId { get; set; }

        public ExternalIdentityMapping ToDomain() => ExternalIdentityMapping.Rehydrate(
            ProviderKey,
            ExternalUserId,
            CommunityIdentityId);
    }
}
