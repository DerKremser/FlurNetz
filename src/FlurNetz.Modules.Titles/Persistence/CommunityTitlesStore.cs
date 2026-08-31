using Dapper;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Titles.Persistence;

/// <summary>
/// Persistiert den vollständigen Community-Titelzustand atomar in PostgreSQL.
/// </summary>
/// <remarks>
/// Die Root-Zeile in <c>community_titles</c> ist der Lock-Anker. Jede Read/Modify/Write-
/// Sequenz sperrt sie mit <c>SELECT FOR UPDATE</c>, sodass Änderungen derselben Community
/// serialisiert werden, während andere Communities unabhängig bleiben.
/// </remarks>
public sealed class CommunityTitlesStore : ICommunityTitlesStore
{
    private const string EnsureRootSql = """
        INSERT INTO community_titles
            (community_identity_id)
        VALUES
            (@CommunityIdentityId)
        ON CONFLICT (community_identity_id) DO NOTHING;
        """;

    private const string LockRootSql = """
        SELECT community_identity_id
        FROM community_titles
        WHERE community_identity_id = @CommunityIdentityId
        FOR UPDATE;
        """;

    private const string SelectUnlocksSql = """
        SELECT title_definition_id
        FROM community_title_unlocks
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private const string SelectCurrentSql = """
        SELECT title_definition_id AS TitleDefinitionId
        FROM community_title_selections
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private const string InsertUnlockSql = """
        INSERT INTO community_title_unlocks
            (community_identity_id, title_definition_id)
        VALUES
            (@CommunityIdentityId, @TitleDefinitionId);
        """;

    private const string DeleteSelectionSql = """
        DELETE FROM community_title_selections
        WHERE community_identity_id = @CommunityIdentityId;
        """;

    private const string UpsertSelectionSql = """
        INSERT INTO community_title_selections
            (community_identity_id, title_definition_id)
        VALUES
            (@CommunityIdentityId, @TitleDefinitionId)
        ON CONFLICT (community_identity_id)
        DO UPDATE
        SET title_definition_id = EXCLUDED.title_definition_id;
        """;

    private const string DeleteUnlockSql = """
        DELETE FROM community_title_unlocks
        WHERE community_identity_id = @CommunityIdentityId
          AND title_definition_id = @TitleDefinitionId;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Store mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    public CommunityTitlesStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        CommunityIdentityId communityIdentityId,
        Func<CommunityTitles, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);
            var parameters = new
            {
                CommunityIdentityId = validCommunityIdentityId.Value
            };

            await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        EnsureRootSql,
                        parameters,
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await transaction.Connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        LockRootSql,
                        parameters,
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var persistedUnlocks = (await transaction.Connection.QueryAsync<Guid>(
                    new CommandDefinition(
                        SelectUnlocksSql,
                        parameters,
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false))
                .ToArray();

            var persistedSelection = await transaction.Connection.QuerySingleOrDefaultAsync<SelectionRow>(
                    new CommandDefinition(
                        SelectCurrentSql,
                        parameters,
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var unlockedTitleDefinitionIds = persistedUnlocks
                .Select(TitleDefinitionId.Create)
                .ToArray();
            var currentTitleDefinitionId = persistedSelection is null
                ? (TitleDefinitionId?)null
                : TitleDefinitionId.Create(persistedSelection.TitleDefinitionId);

            var titles = CommunityTitles.Rehydrate(
                validCommunityIdentityId,
                unlockedTitleDefinitionIds,
                currentTitleDefinitionId);
            var before = Snapshot(titles);
            var result = operation(titles);
            var after = Snapshot(titles);

            await PersistChangesAsync(
                    validCommunityIdentityId,
                    before,
                    after,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static TitlesSnapshot Snapshot(CommunityTitles titles) =>
        new(
            titles.UnlockedTitleDefinitionIds.ToHashSet(),
            titles.CurrentTitleDefinitionId);

    private static async Task PersistChangesAsync(
        CommunityIdentityId communityIdentityId,
        TitlesSnapshot before,
        TitlesSnapshot after,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var addedUnlocks = after.UnlockedTitleDefinitionIds
            .Except(before.UnlockedTitleDefinitionIds)
            .ToArray();
        var removedUnlocks = before.UnlockedTitleDefinitionIds
            .Except(after.UnlockedTitleDefinitionIds)
            .ToArray();

        foreach (var titleDefinitionId in addedUnlocks)
        {
            var insertedRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        InsertUnlockSql,
                        new
                        {
                            CommunityIdentityId = communityIdentityId.Value,
                            TitleDefinitionId = titleDefinitionId.Value
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            EnsureAffectedRows(insertedRows, "eingefügt");
        }

        await SynchronizeSelectionAsync(
                communityIdentityId,
                before.CurrentTitleDefinitionId,
                after.CurrentTitleDefinitionId,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var titleDefinitionId in removedUnlocks)
        {
            var deletedRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        DeleteUnlockSql,
                        new
                        {
                            CommunityIdentityId = communityIdentityId.Value,
                            TitleDefinitionId = titleDefinitionId.Value
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            EnsureAffectedRows(deletedRows, "gelöscht");
        }
    }

    private static async Task SynchronizeSelectionAsync(
        CommunityIdentityId communityIdentityId,
        TitleDefinitionId? before,
        TitleDefinitionId? after,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (before is not null && after is null)
        {
            var deletedRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        DeleteSelectionSql,
                        new { CommunityIdentityId = communityIdentityId.Value },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            EnsureAffectedRows(deletedRows, "gelöscht");
            return;
        }

        if (after is { } current && before != current)
        {
            var affectedRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        UpsertSelectionSql,
                        new
                        {
                            CommunityIdentityId = communityIdentityId.Value,
                            TitleDefinitionId = current.Value
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            EnsureAffectedRows(affectedRows, "synchronisiert");
        }
    }

    private static void EnsureAffectedRows(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Die Community-Titles-Zeile konnte nicht eindeutig {operation} werden.");
        }
    }

    private sealed record TitlesSnapshot(
        HashSet<TitleDefinitionId> UnlockedTitleDefinitionIds,
        TitleDefinitionId? CurrentTitleDefinitionId);

    private sealed class SelectionRow
    {
        public Guid TitleDefinitionId { get; set; }
    }
}
