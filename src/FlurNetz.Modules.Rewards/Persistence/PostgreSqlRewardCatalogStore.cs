using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Rewards.Persistence;

/// <summary>
/// Persistiert die aktuell benötigte Reward-Konfiguration in PostgreSQL.
/// </summary>
/// <remarks>
/// Package und Membership werden in einer gemeinsamen Transaktion geschrieben. Der Store
/// bleibt auf die fachlich benötigten Katalogoperationen beschränkt und ist kein generisches
/// Repository.
/// </remarks>
public sealed class PostgreSqlRewardCatalogStore : IRewardCatalogStore
{
    private const string AddDefinitionSql = """
        INSERT INTO reward_definitions
            (id, definition_type, amount)
        VALUES
            (@Id, @DefinitionType, @Amount);
        """;

    private const string DefinitionExistsSql = """
        SELECT EXISTS
        (
            SELECT 1
            FROM reward_definitions
            WHERE id = @DefinitionId
        );
        """;

    private const string AddPackageSql = """
        INSERT INTO reward_packages (id)
        VALUES (@Id);
        """;

    private const string AddPackageDefinitionSql = """
        INSERT INTO reward_package_definitions
            (reward_package_id, reward_definition_id)
        VALUES
            (@RewardPackageId, @RewardDefinitionId);
        """;

    private const string ListDefinitionsSql = """
        SELECT id AS RewardDefinitionId, definition_type AS DefinitionType, amount AS Amount
        FROM reward_definitions
        ORDER BY id;
        """;

    private const string ListPackagesSql = """
        SELECT reward_package_id AS RewardPackageId, reward_definition_id AS RewardDefinitionId
        FROM reward_package_definitions
        ORDER BY reward_package_id, reward_definition_id;
        """;

    private const string ListGrantsSql = """
        SELECT id AS Id, community_identity_id AS CommunityIdentityId,
               reward_definition_id AS RewardDefinitionId, source_type AS SourceType, source_id AS SourceId
        FROM reward_grants
        WHERE @CommunityIdentityId IS NULL OR community_identity_id = @CommunityIdentityId
        ORDER BY id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den PostgreSQL-Katalog-Store.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="connectionFactory"/> fehlt.</exception>
    public PostgreSqlRewardCatalogStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddDefinitionAsync(
        EconomyBalanceRewardDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AddDefinitionAsync(definition, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task AddDefinitionAsync(
        EconomyBalanceRewardDefinition definition,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return connection.ExecuteAsync(new CommandDefinition(
            AddDefinitionSql,
            new { Id = definition.Id.Value, DefinitionType = RewardDefinitionTypeCodes.EconomyBalance, definition.Amount },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RewardDefinitionId>> FindMissingDefinitionIdsAsync(
        IEnumerable<RewardDefinitionId> rewardDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rewardDefinitionIds);

        var missingDefinitionIds = new List<RewardDefinitionId>();
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var rewardDefinitionId in rewardDefinitionIds)
        {
            var exists = await connection.QuerySingleAsync<bool>(
                    new CommandDefinition(
                        DefinitionExistsSql,
                        new { DefinitionId = rewardDefinitionId.Value },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (!exists)
            {
                missingDefinitionIds.Add(rewardDefinitionId);
            }
        }

        return Array.AsReadOnly(missingDefinitionIds.ToArray());
    }

    /// <inheritdoc />
    public async Task AddPackageAsync(
        RewardPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AddPackageAsync(package, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task AddPackageAsync(
        RewardPackage package,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var missingDefinitionIds = await FindMissingDefinitionIdsAsync(package.RewardDefinitionIds, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (missingDefinitionIds.Count != 0) throw new RewardDefinitionNotFoundException(missingDefinitionIds);

        await connection.ExecuteAsync(new CommandDefinition(
            AddPackageSql,
            new { Id = package.Id.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var rewardDefinitionId in package.RewardDefinitionIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                AddPackageDefinitionSql,
                new { RewardPackageId = package.Id.Value, RewardDefinitionId = rewardDefinitionId.Value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<RewardDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RewardDefinitionRow>(new CommandDefinition(ListDefinitionsSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(ToDefinition).ToArray());
    }

    public async Task<IReadOnlyList<RewardPackage>> ListPackagesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = (await connection.QueryAsync<RewardPackageMembershipRow>(new CommandDefinition(ListPackagesSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
        return Array.AsReadOnly(rows.GroupBy(row => row.RewardPackageId).Select(group => RewardPackage.Create(RewardPackageId.Create(group.Key), group.Select(row => RewardDefinitionId.Create(row.RewardDefinitionId)))).ToArray());
    }

    public async Task<IReadOnlyList<RewardGrant>> ListGrantsAsync(CommunityIdentityId? communityIdentityId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<RewardGrantRow>(new CommandDefinition(ListGrantsSql, new { CommunityIdentityId = communityIdentityId?.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(row => RewardGrant.Create(
            RewardGrantId.Create(row.Id),
            CommunityIdentityId.Create(row.CommunityIdentityId),
            RewardDefinitionId.Create(row.RewardDefinitionId),
            RewardSource.Create(row.SourceType, row.SourceId))).ToArray());
    }

    private static async Task<IReadOnlyList<RewardDefinitionId>> FindMissingDefinitionIdsAsync(
        IEnumerable<RewardDefinitionId> rewardDefinitionIds,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var missingDefinitionIds = new List<RewardDefinitionId>();

        foreach (var rewardDefinitionId in rewardDefinitionIds)
        {
            var exists = await connection.QuerySingleAsync<bool>(
                    new CommandDefinition(
                        DefinitionExistsSql,
                        new { DefinitionId = rewardDefinitionId.Value },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (!exists)
            {
                missingDefinitionIds.Add(rewardDefinitionId);
            }
        }

        return Array.AsReadOnly(missingDefinitionIds.ToArray());
    }

    private static RewardDefinition ToDefinition(RewardDefinitionRow row) => row.DefinitionType switch
    {
        RewardDefinitionTypeCodes.EconomyBalance => EconomyBalanceRewardDefinition.Create(RewardDefinitionId.Create(row.RewardDefinitionId), row.Amount),
        _ => throw new InvalidOperationException($"Der persistierte Reward-Definition-Typ '{row.DefinitionType}' wird nicht unterstützt.")
    };

    private sealed class RewardPackageMembershipRow
    {
        public Guid RewardPackageId { get; set; }
        public Guid RewardDefinitionId { get; set; }
    }

    private sealed class RewardGrantRow
    {
        public Guid Id { get; set; }
        public Guid CommunityIdentityId { get; set; }
        public Guid RewardDefinitionId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
    }

    private sealed class RewardDefinitionRow
    {
        public Guid RewardDefinitionId { get; set; }
        public string DefinitionType { get; set; } = string.Empty;
        public long Amount { get; set; }
    }
}
