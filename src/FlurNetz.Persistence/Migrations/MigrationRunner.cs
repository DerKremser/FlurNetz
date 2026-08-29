using Dapper;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Führt explizite SQL-Migrationen gegen PostgreSQL aus und verwaltet deren technische History.
/// </summary>
/// <remarks>
/// Der Runner startet nicht automatisch. Der Host entscheidet ausdrücklich, wann ein Lauf
/// stattfindet. Jede neue Migration und ihr History-Eintrag werden in derselben Transaktion
/// ausgeführt; ein Fehler kann daher keine Migration als angewendet registrieren.
/// </remarks>
public sealed class MigrationRunner
{
    /// <summary>
    /// Voll qualifizierter Name der technischen Tabelle für angewendete Migrationen.
    /// </summary>
    public const string MigrationHistoryTableName = "flurnetz_persistence.migration_history";

    private const string CreateHistorySql = """
        CREATE SCHEMA IF NOT EXISTS flurnetz_persistence;

        CREATE TABLE IF NOT EXISTS flurnetz_persistence.migration_history
        (
            owner text NOT NULL,
            version bigint NOT NULL,
            name text NOT NULL,
            applied_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            checksum text NOT NULL,
            CONSTRAINT pk_migration_history PRIMARY KEY (owner, version)
        );
        """;

    private const string ReadHistorySql = """
        SELECT owner, version, name, checksum
        FROM flurnetz_persistence.migration_history;
        """;

    private const string RegisterMigrationSql = """
        INSERT INTO flurnetz_persistence.migration_history (owner, version, name, checksum)
        VALUES (@Owner, @Version, @Name, @Checksum);
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly IReadOnlyList<IMigrationSource> migrationSources;

    /// <summary>
    /// Erstellt einen Runner für eine einzelne Migrationsquelle.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für PostgreSQL-Verbindungen.</param>
    /// <param name="migrationSource">Quelle der expliziten SQL-Migrationen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Verbindungsfabrik fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn die Migrationsquelle fehlt.</exception>
    public MigrationRunner(IPostgreSqlConnectionFactory connectionFactory, IMigrationSource migrationSource)
        : this(connectionFactory, [migrationSource])
    {
    }

    /// <summary>
    /// Erstellt einen Runner für mehrere explizit registrierte Migrationsquellen.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für PostgreSQL-Verbindungen.</param>
    /// <param name="migrationSources">Die zu sammelnden Migrationsquellen.</param>
    /// <exception cref="ArgumentNullException">Wenn eine Sammlung oder die Verbindungsfabrik fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn eine Sammlung eine Null-Quelle enthält.</exception>
    public MigrationRunner(
        IPostgreSqlConnectionFactory connectionFactory,
        IEnumerable<IMigrationSource> migrationSources)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(migrationSources);

        this.connectionFactory = connectionFactory;
        this.migrationSources = migrationSources.ToArray();

        if (this.migrationSources.Any(source => source is null))
        {
            throw new ArgumentException("A migration source cannot be null.", nameof(migrationSources));
        }
    }

    /// <summary>
    /// Führt alle noch fehlenden Migrationen in deterministischer Reihenfolge aus.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen vor oder zwischen Migrationen.</param>
    /// <returns>Anzahl angewendeter und übersprungener Migrationen.</returns>
    /// <exception cref="InvalidOperationException">Wenn eine Migration doppelt ist oder nach Anwendung geändert wurde.</exception>
    /// <remarks>
    /// Das Anlegen der History ist selbst transaktional. Jede einzelne Migration besitzt
    /// anschließend eine eigene Transaktion, sodass ein Fehler die vorherigen erfolgreichen
    /// Migrationen nicht zurückrollt.
    /// </remarks>
    public async Task<MigrationRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        // Erst vollständig validieren und sortieren, bevor Schema oder History verändert werden.
        var migrations = MigrationOrdering.Order(
            migrationSources.SelectMany(source => source.GetMigrations()));

        await EnsureHistoryAsync(cancellationToken).ConfigureAwait(false);
        var appliedMigrations = await ReadAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);

        var appliedCount = 0;
        var skippedCount = 0;

        foreach (var migration in migrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = new MigrationKey(migration.Owner, migration.Version);
            if (appliedMigrations.TryGetValue(key, out var appliedMigration))
            {
                EnsureMigrationIsUnchanged(migration, appliedMigration);
                skippedCount++;
                continue;
            }

            await ApplyMigrationAsync(migration, cancellationToken).ConfigureAwait(false);
            appliedMigrations.Add(key, new AppliedMigration(migration.Name, MigrationChecksum.Compute(migration.Sql)));
            appliedCount++;
        }

        return new MigrationRunResult(appliedCount, skippedCount);
    }

    private async Task EnsureHistoryAsync(CancellationToken cancellationToken)
    {
        // Schema und History-Tabelle werden gemeinsam angelegt, damit ein halbfertiger Metadatenzustand vermieden wird.
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    CreateHistorySql,
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<MigrationKey, AppliedMigration>> ReadAppliedMigrationsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<MigrationHistoryRow>(
                new CommandDefinition(ReadHistorySql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => new MigrationKey(row.Owner, row.Version),
            row => new AppliedMigration(row.Name, row.Checksum));
    }

    private async Task ApplyMigrationAsync(Migration migration, CancellationToken cancellationToken)
    {
        // SQL und History-Eintrag teilen bewusst dieselbe Verbindung und Transaktion.
        // So bleibt eine fehlgeschlagene Migration unsichtbar für spätere Läufe.
        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        var checksum = MigrationChecksum.Compute(migration.Sql);

        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    migration.Sql,
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await transaction.Connection.ExecuteAsync(
                new CommandDefinition(
                    RegisterMigrationSql,
                    new
                    {
                        migration.Owner,
                        migration.Version,
                        migration.Name,
                        Checksum = checksum
                    },
                    transaction: transaction.Transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureMigrationIsUnchanged(Migration migration, AppliedMigration appliedMigration)
    {
        // Der Hash schützt die Unveränderlichkeit des bereits ausgeführten exakten SQL-Texts.
        var checksum = MigrationChecksum.Compute(migration.Sql);
        if (!string.Equals(appliedMigration.Name, migration.Name, StringComparison.Ordinal)
            || !string.Equals(appliedMigration.Checksum, checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Applied migration '{migration.Identity}' was changed. Applied migrations are immutable.");
        }
    }

    private readonly record struct MigrationKey(string Owner, long Version);

    private sealed record AppliedMigration(string Name, string Checksum);

    private sealed class MigrationHistoryRow
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
