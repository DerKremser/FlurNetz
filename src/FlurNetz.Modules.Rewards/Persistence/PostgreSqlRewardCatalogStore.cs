using Dapper;
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
            await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        AddDefinitionSql,
                        new
                        {
                            Id = definition.Id.Value,
                            DefinitionType = RewardDefinitionTypeCodes.EconomyBalance,
                            definition.Amount
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
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
            var missingDefinitionIds = await FindMissingDefinitionIdsAsync(
                    package.RewardDefinitionIds,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            if (missingDefinitionIds.Count != 0)
            {
                throw new RewardDefinitionNotFoundException(missingDefinitionIds);
            }

            await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        AddPackageSql,
                        new { Id = package.Id.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            foreach (var rewardDefinitionId in package.RewardDefinitionIds)
            {
                await transaction.Connection.ExecuteAsync(
                        new CommandDefinition(
                            AddPackageDefinitionSql,
                            new
                            {
                                RewardPackageId = package.Id.Value,
                                RewardDefinitionId = rewardDefinitionId.Value
                            },
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
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
}
