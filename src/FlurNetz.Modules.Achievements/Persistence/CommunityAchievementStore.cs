using Dapper;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;

namespace FlurNetz.Modules.Achievements.Persistence;

/// <summary>
/// Persistiert permanente Community-Achievements mit einem atomaren PostgreSQL-Insert.
/// </summary>
/// <remarks>
/// Die Composite-Primary-Key-Kollision entscheidet direkt über den booleschen Rückgabewert.
/// Es gibt weder eine vorgelagerte Existenzabfrage noch eine globale Sperre pro Community.
/// </remarks>
public sealed class CommunityAchievementStore : ICommunityAchievementStore
{
    private const string UnlockSql = """
        INSERT INTO community_achievements
            (community_identity_id, achievement_definition_id, unlocked_at_utc)
        VALUES
            (@CommunityIdentityId, @AchievementDefinitionId, @UnlockedAtUtc)
        ON CONFLICT (community_identity_id, achievement_definition_id) DO NOTHING;
        """;

    private const string GetSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            achievement_definition_id AS AchievementDefinitionId,
            unlocked_at_utc AS UnlockedAtUtc
        FROM community_achievements
        WHERE community_identity_id = @CommunityIdentityId
          AND achievement_definition_id = @AchievementDefinitionId;
        """;

    private const string ListSql = """
        SELECT
            community_identity_id AS CommunityIdentityId,
            achievement_definition_id AS AchievementDefinitionId,
            unlocked_at_utc AS UnlockedAtUtc
        FROM community_achievements
        WHERE community_identity_id = @CommunityIdentityId
        ORDER BY unlocked_at_utc ASC, achievement_definition_id ASC;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Community-Achievement-Store.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    public CommunityAchievementStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<bool> UnlockAsync(
        CommunityAchievement achievement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(achievement);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await UnlockAsync(achievement, connection, transaction: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UnlockAsync(
        CommunityAchievement achievement,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(achievement);
        ArgumentNullException.ThrowIfNull(connection);
        var affectedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    UnlockSql,
                    new
                    {
                        CommunityIdentityId = achievement.CommunityIdentityId.Value,
                        AchievementDefinitionId = achievement.AchievementDefinitionId.Value,
                        achievement.UnlockedAtUtc
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return affectedRows switch
        {
            1 => true,
            0 => false,
            _ => throw new InvalidOperationException(
                "Der Community-Achievement-Unlock hat unerwartet mehrere Zeilen verändert.")
        };
    }

    /// <inheritdoc />
    public async Task<CommunityAchievement?> GetAsync(
        CommunityIdentityId communityIdentityId,
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validAchievementDefinitionId = AchievementDefinitionId.Create(achievementDefinitionId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<CommunityAchievementRow>(
                new CommandDefinition(
                    GetSql,
                    new
                    {
                        CommunityIdentityId = validCommunityIdentityId.Value,
                        AchievementDefinitionId = validAchievementDefinitionId.Value
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommunityAchievement>> ListAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<CommunityAchievementRow>(
                new CommandDefinition(
                    ListSql,
                    new { CommunityIdentityId = validCommunityIdentityId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    private static CommunityAchievement Rehydrate(CommunityAchievementRow row)
    {
        return CommunityAchievement.Rehydrate(
            CommunityIdentityId.Create(row.CommunityIdentityId),
            AchievementDefinitionId.Create(row.AchievementDefinitionId),
            row.UnlockedAtUtc);
    }

    private sealed class CommunityAchievementRow
    {
        public Guid CommunityIdentityId { get; set; }

        public Guid AchievementDefinitionId { get; set; }

        public DateTimeOffset UnlockedAtUtc { get; set; }
    }
}
