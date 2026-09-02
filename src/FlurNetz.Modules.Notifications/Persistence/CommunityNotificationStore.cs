using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using System.Data.Common;

namespace FlurNetz.Modules.Notifications.Persistence;

/// <summary>
/// Persistiert persönliche Notifications mit gezielten PostgreSQL-/Dapper-Queries.
/// </summary>
/// <remarks>
/// Die transaction-aware Insert-Operation führt keinen Commit aus. Dadurch kann der
/// Messaging-Consumer den Notification-Write gemeinsam mit seiner Inbox-Markierung bestätigen.
/// </remarks>
public sealed class CommunityNotificationStore : ICommunityNotificationStore
{
    private const string InsertSql = """
        INSERT INTO community_notifications
            (id, community_identity_id, notification_type, title, message,
             source_type, source_id, created_at_utc, read_at_utc)
        VALUES
            (@Id, @CommunityIdentityId, @NotificationType, @Title, @Message,
             @SourceType, @SourceId, @CreatedAtUtc, @ReadAtUtc);
        """;

    private const string GetSql = """
        SELECT
            id AS Id,
            community_identity_id AS CommunityIdentityId,
            notification_type AS NotificationType,
            title AS Title,
            message AS Message,
            source_type AS SourceType,
            source_id AS SourceId,
            created_at_utc AS CreatedAtUtc,
            read_at_utc AS ReadAtUtc
        FROM community_notifications
        WHERE id = @Id;
        """;

    private const string GetForIdentitySql = """
        SELECT
            id AS Id,
            community_identity_id AS CommunityIdentityId,
            notification_type AS NotificationType,
            title AS Title,
            message AS Message,
            source_type AS SourceType,
            source_id AS SourceId,
            created_at_utc AS CreatedAtUtc,
            read_at_utc AS ReadAtUtc
        FROM community_notifications
        WHERE community_identity_id = @CommunityIdentityId
          AND id = @Id;
        """;

    private const string ListSql = """
        SELECT
            id AS Id,
            community_identity_id AS CommunityIdentityId,
            notification_type AS NotificationType,
            title AS Title,
            message AS Message,
            source_type AS SourceType,
            source_id AS SourceId,
            created_at_utc AS CreatedAtUtc,
            read_at_utc AS ReadAtUtc
        FROM community_notifications
        WHERE community_identity_id = @CommunityIdentityId
          AND (@UnreadOnly = FALSE OR read_at_utc IS NULL)
          AND (
              @HasCursor = FALSE
              OR created_at_utc < @CreatedAtUtc
              OR (created_at_utc = @CreatedAtUtc AND id < @NotificationId)
          )
        ORDER BY created_at_utc DESC, id DESC
        LIMIT @Take;
        """;

    private const string CountUnreadSql = """
        SELECT COUNT(*)::bigint
        FROM community_notifications
        WHERE community_identity_id = @CommunityIdentityId
          AND read_at_utc IS NULL;
        """;

    private const string MarkReadSql = """
        UPDATE community_notifications
        SET read_at_utc = COALESCE(read_at_utc, @ReadAtUtc)
        WHERE community_identity_id = @CommunityIdentityId
          AND id = @Id
        RETURNING id;
        """;

    private const string MarkUnreadSql = """
        UPDATE community_notifications
        SET read_at_utc = NULL
        WHERE community_identity_id = @CommunityIdentityId
          AND id = @Id
        RETURNING id;
        """;

    private const string MarkAllReadSql = """
        UPDATE community_notifications
        SET read_at_utc = @ReadAtUtc
        WHERE community_identity_id = @CommunityIdentityId
          AND read_at_utc IS NULL;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    public CommunityNotificationStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        CommunityNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AddAsync(
                    notification,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task AddAsync(
        CommunityNotification notification,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var insertedRows = await connection.ExecuteAsync(
                new CommandDefinition(
                    InsertSql,
                    new
                    {
                        Id = notification.Id.Value,
                        CommunityIdentityId = notification.CommunityIdentityId.Value,
                        notification.NotificationType,
                        notification.Title,
                        notification.Message,
                        SourceType = notification.SourceReference?.SourceType,
                        SourceId = notification.SourceReference?.SourceId,
                        notification.CreatedAtUtc,
                        notification.ReadAtUtc
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (insertedRows != 1)
        {
            throw new InvalidOperationException(
                "Die Community-Notification konnte nicht eindeutig persistiert werden.");
        }
    }

    public async Task<CommunityNotification?> GetAsync(
        NotificationId notificationId,
        CancellationToken cancellationToken = default)
    {
        var validNotificationId = NotificationId.Create(notificationId.Value);
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<NotificationRow>(
                new CommandDefinition(
                    GetSql,
                    new { Id = validNotificationId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    public async Task<CommunityNotification?> GetForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validNotificationId = NotificationId.Create(notificationId.Value);
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<NotificationRow>(
                new CommandDefinition(
                    GetForIdentitySql,
                    new
                    {
                        CommunityIdentityId = validIdentityId.Value,
                        Id = validNotificationId.Value
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    public async Task<IReadOnlyList<CommunityNotification>> ListForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        NotificationInboxCursor? cursor,
        bool unreadOnly,
        int take,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        if (take < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                "Die Anzahl der zu lesenden Notifications muss größer als null sein.");
        }

        if (cursor is not null
            && (cursor.CommunityIdentityId != validIdentityId || cursor.UnreadOnly != unreadOnly))
        {
            throw new ArgumentException(
                "Der Notification-Cursor gehört nicht zur angefragten Identität oder zum Filter.",
                nameof(cursor));
        }

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<NotificationRow>(
                new CommandDefinition(
                    ListSql,
                    new
                    {
                        CommunityIdentityId = validIdentityId.Value,
                        UnreadOnly = unreadOnly,
                        HasCursor = cursor is not null,
                        CreatedAtUtc = cursor?.CreatedAtUtc,
                        NotificationId = cursor?.NotificationId.Value,
                        Take = take
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    public async Task<long> CountUnreadForIdentityAsync(
        CommunityIdentityId communityIdentityId,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    CountUnreadSql,
                    new { CommunityIdentityId = validIdentityId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> MarkReadAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validNotificationId = NotificationId.Create(notificationId.Value);
        EnsureValidUtc(readAtUtc, nameof(readAtUtc));
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    MarkReadSql,
                    new
                    {
                        CommunityIdentityId = validIdentityId.Value,
                        Id = validNotificationId.Value,
                        ReadAtUtc = readAtUtc
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return id.HasValue;
    }

    public async Task<bool> MarkUnreadAsync(
        CommunityIdentityId communityIdentityId,
        NotificationId notificationId,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        var validNotificationId = NotificationId.Create(notificationId.Value);
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    MarkUnreadSql,
                    new
                    {
                        CommunityIdentityId = validIdentityId.Value,
                        Id = validNotificationId.Value
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return id.HasValue;
    }

    public async Task<long> MarkAllReadAsync(
        CommunityIdentityId communityIdentityId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default)
    {
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
        EnsureValidUtc(readAtUtc, nameof(readAtUtc));
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection.ExecuteAsync(
                new CommandDefinition(
                    MarkAllReadSql,
                    new
                    {
                        CommunityIdentityId = validIdentityId.Value,
                        ReadAtUtc = readAtUtc
                    },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static CommunityNotification Rehydrate(NotificationRow row)
    {
        if ((row.SourceType is null) != (row.SourceId is null))
        {
            throw new InvalidOperationException(
                "Die persistierte Notification-SourceReference ist inkonsistent.");
        }

        NotificationSourceReference? sourceReference = null;
        if (row.SourceType is not null && row.SourceId is not null)
        {
            sourceReference = NotificationSourceReference.Rehydrate(
                row.SourceType,
                row.SourceId);
        }

        return CommunityNotification.Rehydrate(
            NotificationId.Create(row.Id),
            CommunityIdentityId.Create(row.CommunityIdentityId),
            row.NotificationType,
            row.Title,
            row.Message,
            sourceReference,
            row.CreatedAtUtc.ToUniversalTime(),
            row.ReadAtUtc?.ToUniversalTime());
    }

    private static void EnsureValidUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Notification-Zeitpunkt muss in UTC vorliegen.", parameterName);
        }

        if (value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Notification-Zeitpunkt muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                parameterName);
        }
    }

    private sealed class NotificationRow
    {
        public Guid Id { get; set; }
        public Guid CommunityIdentityId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? ReadAtUtc { get; set; }
    }
}
