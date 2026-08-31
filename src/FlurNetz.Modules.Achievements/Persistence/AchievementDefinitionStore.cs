using Dapper;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Achievements.Persistence;

/// <summary>
/// Persistiert Achievement-Definitionen mit gezielten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Mutationen laden die Definition mit <c>SELECT ... FOR UPDATE</c>, führen den synchronen
/// Domain-Callback aus und schreiben nur bei einer tatsächlichen Änderung zurück.
/// </remarks>
public sealed class AchievementDefinitionStore : IAchievementDefinitionStore
{
    private const string AddSql = """
        INSERT INTO achievement_definitions
            (id, display_name, description)
        VALUES
            (@Id, @DisplayName, @Description);
        """;

    private const string GetSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM achievement_definitions
        WHERE id = @Id;
        """;

    private const string ListSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM achievement_definitions
        ORDER BY id;
        """;

    private const string GetForUpdateSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM achievement_definitions
        WHERE id = @Id
        FOR UPDATE;
        """;

    private const string UpdateSql = """
        UPDATE achievement_definitions
        SET
            display_name = @DisplayName,
            description = @Description
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Katalog-Store mit der technischen Verbindungsfabrik.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für geöffnete PostgreSQL-Verbindungen.</param>
    public AchievementDefinitionStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        AchievementDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var affectedRows = await transaction.Connection.ExecuteAsync(
                    new CommandDefinition(
                        AddSql,
                        new
                        {
                            Id = definition.Id.Value,
                            definition.DisplayName,
                            definition.Description
                        },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            EnsureAffectedRows(affectedRows, "eingefügt");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AchievementDefinition?> GetAsync(
        AchievementDefinitionId achievementDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var validId = AchievementDefinitionId.Create(achievementDefinitionId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<AchievementDefinitionRow>(
                new CommandDefinition(
                    GetSql,
                    new { Id = validId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AchievementDefinition>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<AchievementDefinitionRow>(
                new CommandDefinition(
                    ListSql,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        AchievementDefinitionId achievementDefinitionId,
        Func<AchievementDefinition, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var validId = AchievementDefinitionId.Create(achievementDefinitionId.Value);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var row = await transaction.Connection.QuerySingleOrDefaultAsync<AchievementDefinitionRow>(
                    new CommandDefinition(
                        GetForUpdateSql,
                        new { Id = validId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (row is null)
            {
                throw new AchievementDefinitionNotFoundException(validId);
            }

            var definition = Rehydrate(row);
            var before = Snapshot(definition);
            var result = operation(definition);
            var after = Snapshot(definition);

            if (before != after)
            {
                var affectedRows = await transaction.Connection.ExecuteAsync(
                        new CommandDefinition(
                            UpdateSql,
                            new
                            {
                                Id = validId.Value,
                                after.DisplayName,
                                after.Description
                            },
                            transaction: transaction.Transaction,
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                EnsureAffectedRows(affectedRows, "aktualisiert");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static AchievementDefinition Rehydrate(AchievementDefinitionRow row)
    {
        return AchievementDefinition.Rehydrate(
            AchievementDefinitionId.Create(row.Id),
            row.DisplayName,
            row.Description);
    }

    private static DefinitionSnapshot Snapshot(AchievementDefinition definition) =>
        new(definition.DisplayName, definition.Description);

    private static void EnsureAffectedRows(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Die Achievement-Definition konnte nicht eindeutig {operation} werden.");
        }
    }

    private sealed record DefinitionSnapshot(string DisplayName, string? Description);

    private sealed class AchievementDefinitionRow
    {
        public Guid Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
