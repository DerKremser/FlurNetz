using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Progression.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Progression.Persistence;

/// <summary>
/// Persistiert Community-Progressionen mit atomaren, parametrisierten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Der Grant-Pfad initialisiert eine fehlende Zeile mit <c>ON CONFLICT DO NOTHING</c>,
/// sperrt anschließend genau die Progressionszeile mit <c>FOR UPDATE</c>, rehydriert die
/// Domain und persistiert die von ihr berechnete Summe. Der gesamte Read/Modify/Write-
/// Vorgang bleibt in derselben <see cref="PostgreSqlTransaction"/>.
/// </remarks>
public sealed class CommunityProgressionStore : ICommunityProgressionStore
{
    private const string InitializeSql = """
        INSERT INTO community_progressions
            (community_identity_id, experience_points)
        VALUES
            (@CommunityIdentityId, 0)
        ON CONFLICT (community_identity_id) DO NOTHING;
        """;

    private const string SelectForUpdateSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            experience_points AS ExperiencePoints
        FROM community_progressions
        WHERE community_identity_id = @CommunityIdentityId
        FOR UPDATE;
        """;

    private const string SelectSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            experience_points AS ExperiencePoints
        FROM community_progressions
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private const string UpdateSql = """
        UPDATE community_progressions
        SET experience_points = @ExperiencePoints
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Store mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    public CommunityProgressionStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<ExperiencePoints> GrantExperienceAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        InitializeSql,
                        new { CommunityIdentityId = communityIdentityId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var row = await transaction.Connection.QuerySingleAsync<CommunityProgressionRow>(
                    new CommandDefinition(
                        SelectForUpdateSql,
                        new { CommunityIdentityId = communityIdentityId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var progression = CommunityProgression.Rehydrate(
                CommunityIdentityId.Create(row.CommunityIdentityId),
                ExperiencePoints.Create(row.ExperiencePoints));
            progression.GrantExperience(amount);

            var updatedRows = await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        UpdateSql,
                        new
                        {
                            CommunityIdentityId = progression.CommunityIdentityId.Value,
                            ExperiencePoints = progression.ExperiencePoints.Value
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (updatedRows != 1)
            {
                throw new InvalidOperationException(
                    "Der Community-Progressionszustand konnte nicht eindeutig aktualisiert werden.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return progression.ExperiencePoints;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommunityProgression?> GetByCommunityIdentityIdAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<CommunityProgressionRow>(
                new CommandDefinition(
                    SelectSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return CommunityProgression.Rehydrate(
            CommunityIdentityId.Create(row.CommunityIdentityId),
            ExperiencePoints.Create(row.ExperiencePoints));
    }

    private sealed class CommunityProgressionRow
    {
        public Guid CommunityIdentityId { get; set; }

        public long ExperiencePoints { get; set; }
    }
}
