using Dapper;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Titles.Persistence;

/// <summary>
/// Persistiert Title-Definitionen mit gezielten PostgreSQL-Operationen.
/// </summary>
/// <remarks>
/// Katalogmutationen laden die Definition mit <c>SELECT FOR UPDATE</c>. Damit werden
/// parallele Änderungen derselben Definition serialisiert, während unterschiedliche
/// Definitionen unabhängig voneinander mutiert werden können.
/// </remarks>
public sealed class TitleDefinitionStore : ITitleDefinitionStore
{
    private const string AddSql = """
        INSERT INTO title_definitions
            (id, display_name, description)
        VALUES
            (@Id, @DisplayName, @Description);
        """;

    private const string GetSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM title_definitions
        WHERE id = @Id;
        """;

    private const string ListSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM title_definitions
        ORDER BY id;
        """;

    private const string GetForUpdateSql = """
        SELECT
            id AS Id,
            display_name AS DisplayName,
            description AS Description
        FROM title_definitions
        WHERE id = @Id
        FOR UPDATE;
        """;

    private const string UpdateSql = """
        UPDATE title_definitions
        SET
            display_name = @DisplayName,
            description = @Description
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>
    /// Erstellt den Katalog-Store mit der technischen Verbindungsfabrik.
    /// </summary>
    public TitleDefinitionStore(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        TitleDefinition definition,
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
    public async Task<TitleDefinition?> GetAsync(
        TitleDefinitionId titleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var validTitleDefinitionId = TitleDefinitionId.Create(titleDefinitionId.Value);

        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<TitleDefinitionRow>(
                new CommandDefinition(
                    GetSql,
                    new { Id = validTitleDefinitionId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TitleDefinition>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<TitleDefinitionRow>(
                new CommandDefinition(
                    ListSql,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        TitleDefinitionId titleDefinitionId,
        Func<TitleDefinition, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var validTitleDefinitionId = TitleDefinitionId.Create(titleDefinitionId.Value);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var row = await transaction.Connection.QuerySingleOrDefaultAsync<TitleDefinitionRow>(
                    new CommandDefinition(
                        GetForUpdateSql,
                        new { Id = validTitleDefinitionId.Value },
                        transaction: transaction.Transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (row is null)
            {
                throw new TitleDefinitionNotFoundException(validTitleDefinitionId);
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
                                Id = validTitleDefinitionId.Value,
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

    private static TitleDefinition Rehydrate(TitleDefinitionRow row)
    {
        return TitleDefinition.Rehydrate(
            TitleDefinitionId.Create(row.Id),
            row.DisplayName,
            row.Description);
    }

    private static TitleDefinitionSnapshot Snapshot(TitleDefinition definition) =>
        new(definition.DisplayName, definition.Description);

    private static void EnsureAffectedRows(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Die Title-Definition konnte nicht eindeutig {operation} werden.");
        }
    }

    private sealed record TitleDefinitionSnapshot(
        string DisplayName,
        string? Description);

    private sealed class TitleDefinitionRow
    {
        public Guid Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
