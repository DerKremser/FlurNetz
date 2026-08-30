using Dapper;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Rewards.Persistence;

/// <summary>
/// Führt Reward-Packages in einer gemeinsamen PostgreSQL-Transaktion aus.
/// </summary>
/// <remarks>
/// Die eindeutige Datenbank-Constraint auf Quelle und Definition ist die authoritative
/// Nebenläufigkeitsgrenze. Grant-Zeilen werden vor den Effects reserviert, bleiben aber bis
/// zum gemeinsamen Commit unbestätigt. So rollen ein fehlgeschlagener Effect, alle Grants
/// und alle Economy-Writes gemeinsam zurück.
/// </remarks>
public sealed class PostgreSqlRewardPackageGrantExecutor : IRewardPackageGrantExecutor
{
    private const string PackageExistsSql = """
        SELECT EXISTS
        (
            SELECT 1
            FROM reward_packages
            WHERE id = @RewardPackageId
        );
        """;

    private const string LoadDefinitionsSql = """
        SELECT
            reward_package_definitions.reward_definition_id AS RewardDefinitionId,
            reward_definitions.definition_type AS DefinitionType,
            reward_definitions.amount AS Amount
        FROM reward_package_definitions
        INNER JOIN reward_definitions
            ON reward_definitions.id = reward_package_definitions.reward_definition_id
        WHERE reward_package_definitions.reward_package_id = @RewardPackageId
        ORDER BY reward_package_definitions.reward_definition_id;
        """;

    private const string ReserveGrantSql = """
        INSERT INTO reward_grants
            (id, community_identity_id, reward_definition_id, source_type, source_id)
        VALUES
            (@Id, @CommunityIdentityId, @RewardDefinitionId, @SourceType, @SourceId)
        ON CONFLICT (source_type, source_id, reward_definition_id) DO NOTHING
        RETURNING id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IEconomyBalanceCredit economyBalanceCredit;

    /// <summary>
    /// Erstellt den atomaren PostgreSQL-Grant-Executor.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <param name="economyBalanceCredit">Neutrale Economy-Credit-Fähigkeit.</param>
    /// <exception cref="ArgumentNullException">Wenn eine Abhängigkeit fehlt.</exception>
    public PostgreSqlRewardPackageGrantExecutor(
        IPostgreSqlConnectionFactory connectionFactory,
        IEconomyBalanceCredit economyBalanceCredit)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(economyBalanceCredit);
        this.connectionFactory = connectionFactory;
        this.economyBalanceCredit = economyBalanceCredit;
    }

    /// <inheritdoc />
    public async Task<RewardPackageGrantOutcome> ExecuteAsync(
        RewardPackageId rewardPackageId,
        CommunityIdentityId communityIdentityId,
        RewardSource source,
        CancellationToken cancellationToken = default)
    {
        var validRewardPackageId = RewardPackageId.Create(rewardPackageId.Value);
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        ArgumentNullException.ThrowIfNull(source);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var packageExists = await transaction.Connection.QuerySingleAsync<bool>(
                    new CommandDefinition(
                        PackageExistsSql,
                        new { RewardPackageId = validRewardPackageId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (!packageExists)
            {
                throw new KeyNotFoundException(
                    $"Das Reward-Package '{validRewardPackageId.Value}' wurde nicht gefunden.");
            }

            var persistedRows = (await transaction.Connection.QueryAsync<RewardDefinitionRow>(
                    new CommandDefinition(
                        LoadDefinitionsSql,
                        new { RewardPackageId = validRewardPackageId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken)))
                .ToArray();

            if (persistedRows.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Das Reward-Package '{validRewardPackageId.Value}' enthält keine Reward-Definition.");
            }

            var definitions = persistedRows
                .Select(ToDomain)
                .ToArray();
            var package = RewardPackage.Create(
                validRewardPackageId,
                definitions.Select(definition => definition.Id));

            var insertedGrantCount = 0;
            foreach (var definition in definitions)
            {
                var grant = RewardGrant.Create(
                    RewardGrantId.New(),
                    validCommunityIdentityId,
                    definition.Id,
                    source);

                var insertedGrantId = await transaction.Connection.QuerySingleOrDefaultAsync<Guid?>(
                        new CommandDefinition(
                            ReserveGrantSql,
                            new
                            {
                                Id = grant.Id.Value,
                                CommunityIdentityId = grant.CommunityIdentityId.Value,
                                RewardDefinitionId = grant.RewardDefinitionId.Value,
                                SourceType = grant.Source.SourceType,
                                SourceId = grant.Source.SourceId
                            },
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                if (insertedGrantId.HasValue)
                {
                    insertedGrantCount++;
                }
            }

            if (insertedGrantCount == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return RewardPackageGrantOutcome.AlreadyGranted;
            }

            if (insertedGrantCount != package.RewardDefinitionIds.Count)
            {
                throw new InvalidOperationException(
                    "Das Reward-Package befindet sich für diese Quelle in einem inkonsistenten "
                    + "Partial-Grant-Zustand; der fehlende Rest wird nicht still ausgeführt.");
            }

            foreach (var definition in definitions)
            {
                if (definition is not EconomyBalanceRewardDefinition economyDefinition)
                {
                    throw new InvalidOperationException(
                        $"Der persistierte Reward-Definition-Typ '{definition.GetType().Name}' "
                        + "wird von diesem Executor nicht unterstützt.");
                }

                await economyBalanceCredit.CreditAsync(
                        validCommunityIdentityId,
                        economyDefinition.Amount,
                        transaction.Connection,
                        transaction.Transaction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return RewardPackageGrantOutcome.Granted;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static RewardDefinition ToDomain(RewardDefinitionRow row)
    {
        var definitionId = RewardDefinitionId.Create(row.RewardDefinitionId);

        return row.DefinitionType switch
        {
            RewardDefinitionTypeCodes.EconomyBalance =>
                EconomyBalanceRewardDefinition.Create(definitionId, row.Amount),
            _ => throw new InvalidOperationException(
                $"Der persistierte Reward-Definition-Typ '{row.DefinitionType}' wird nicht unterstützt.")
        };
    }

    private sealed class RewardDefinitionRow
    {
        public Guid RewardDefinitionId { get; set; }

        public string DefinitionType { get; set; } = string.Empty;

        public long Amount { get; set; }
    }
}
