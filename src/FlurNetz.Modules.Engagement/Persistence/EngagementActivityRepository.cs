using Dapper;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Engagement.Persistence;

/// <summary>
/// Persistiert Engagement-Aktivitäten mit gezieltem parametrisiertem Dapper-SQL in PostgreSQL.
/// </summary>
/// <remarks>
/// Der Adapter besitzt für den normalen Schreibpfad seine Transaktion selbst. Die zusätzliche
/// transaktionsbewusste Überladung erlaubt den expliziten Commit-/Rollback-Test und spätere
/// atomare Kompositionen, ohne eine neue Transaction-Infrastruktur einzuführen.
/// </remarks>
public sealed class EngagementActivityRepository : IEngagementActivityRepository
{
    private const string InsertSql = """
        INSERT INTO engagement_activities
            (id, community_identity_id, activity_type, occurred_at_utc)
        VALUES
            (@Id, @CommunityIdentityId, @ActivityType, @OccurredAtUtc);
        """;

    private const string SelectByIdSql = """
        SELECT
            id AS Id,
            community_identity_id AS CommunityIdentityId,
            activity_type AS ActivityType,
            occurred_at_utc AS OccurredAtUtc
        FROM engagement_activities
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Repository-Adapter mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    public EngagementActivityRepository(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        EngagementActivity activity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AddAsync(activity, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Speichert eine Aktivität innerhalb einer bereits begonnenen PostgreSQL-Transaktion.
    /// </summary>
    /// <param name="activity">Die bereits gültige Message-Aktivität.</param>
    /// <param name="transaction">Die gemeinsame PostgreSQL-Transaktion.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Ein Task nach Ausführung des INSERTs; der Aufrufer entscheidet über Commit oder Rollback.</returns>
    /// <exception cref="ArgumentNullException">Wenn Aktivität oder Transaktion fehlen.</exception>
    public async Task AddAsync(
        EngagementActivity activity,
        PostgreSqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(transaction);

        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    InsertSql,
                    new
                    {
                        Id = activity.Id.Value,
                        CommunityIdentityId = activity.CommunityIdentityId.Value,
                        ActivityType = ToActivityTypeCode(activity.Type),
                        activity.OccurredAtUtc
                    },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EngagementActivity?> GetByIdAsync(
        EngagementActivityId id,
        CancellationToken cancellationToken = default)
    {
        var validId = EngagementActivityId.Create(id.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<EngagementActivityRow>(
                new CommandDefinition(
                    SelectByIdSql,
                    new { Id = validId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var activityType = FromActivityTypeCode(row.ActivityType);
        return activityType switch
        {
            EngagementActivityType.Message => EngagementActivity.CreateMessage(
                EngagementActivityId.Create(row.Id),
                CommunityIdentityId.Create(row.CommunityIdentityId),
                row.OccurredAtUtc),
            _ => throw new InvalidOperationException(
                $"Der Engagement-Aktivitätstyp '{row.ActivityType}' wird nicht unterstützt.")
        };
    }

    private static string ToActivityTypeCode(EngagementActivityType type) => type switch
    {
        EngagementActivityType.Message => "message",
        _ => throw new InvalidOperationException(
            $"Der Engagement-Aktivitätstyp '{type}' besitzt keinen Persistenz-Code.")
    };

    private static EngagementActivityType FromActivityTypeCode(string code) => code switch
    {
        "message" => EngagementActivityType.Message,
        _ => throw new InvalidOperationException(
            $"Der unbekannte persistierte Engagement-Aktivitätstyp '{code}' kann nicht geladen werden.")
    };

    private sealed class EngagementActivityRow
    {
        public Guid Id { get; set; }

        public Guid CommunityIdentityId { get; set; }

        public string ActivityType { get; set; } = string.Empty;

        public DateTimeOffset OccurredAtUtc { get; set; }
    }
}
