using Dapper;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;
using System.Data.Common;

namespace FlurNetz.Modules.Overlay.Persistence;

/// <summary>Persistiert Overlay-Kanäle und technische Source-Key-Hashes.</summary>
public sealed class PostgreSqlOverlayChannelStore : IOverlayChannelStore
{
    private const string Columns = "id AS Id, display_name AS DisplayName, description AS Description, is_enabled AS IsEnabled, is_archived AS IsArchived, created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc, source_key_hash AS SourceKeyHash";
    private const string GetSql = $"SELECT {Columns} FROM overlay_channels WHERE id = @Id;";
    private const string ListSql = $"SELECT {Columns} FROM overlay_channels ORDER BY display_name ASC, id ASC;";
    private const string GetForUpdateSql = $"SELECT {Columns} FROM overlay_channels WHERE id = @Id FOR UPDATE;";
    private const string ResolveSql = $"SELECT {Columns} FROM overlay_channels WHERE source_key_hash = @SourceKeyHash AND is_archived = FALSE;";
    private const string InsertSql = """
        INSERT INTO overlay_channels
            (id, display_name, description, is_enabled, is_archived, created_at_utc, updated_at_utc, source_key_hash)
        VALUES
            (@Id, @DisplayName, @Description, @IsEnabled, @IsArchived, @CreatedAtUtc, @UpdatedAtUtc, @SourceKeyHash);
        """;
    private const string UpdateSql = """
        UPDATE overlay_channels
        SET display_name = @DisplayName,
            description = @Description,
            is_enabled = @IsEnabled,
            is_archived = @IsArchived,
            updated_at_utc = @UpdatedAtUtc,
            source_key_hash = @SourceKeyHash
        WHERE id = @Id;
        """;

    private readonly IPostgreSqlConnectionFactory connectionFactory;

    /// <summary>Erstellt den Kanal-Store.</summary>
    public PostgreSqlOverlayChannelStore(IPostgreSqlConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(OverlayChannel channel, string sourceKeyHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        EnsureHash(sourceKeyHash);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            await AddAsync(channel, sourceKeyHash, transaction.Connection, transaction.Transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<OverlayChannel?> GetAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default)
    {
        var id = EnsureId(channelId);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ChannelRow>(new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OverlayChannel>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ChannelRow>(new CommandDefinition(ListSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(Rehydrate).ToArray());
    }

    /// <inheritdoc />
    public async Task<OverlayChannel?> GetForUpdateAsync(OverlayChannelId channelId, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var id = EnsureId(channelId);
        var row = await connection.QuerySingleOrDefaultAsync<ChannelRow>(new CommandDefinition(GetForUpdateSql, new { Id = id }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<OverlayChannel?> ResolveBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        var hash = OverlaySourceKey.Hash(sourceKey);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ChannelRow>(new CommandDefinition(ResolveSql, new { SourceKeyHash = hash }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Rehydrate(row);
    }

    /// <inheritdoc />
    public async Task<OverlayChannel?> MutateAsync(OverlayChannelId channelId, Func<OverlayChannel, bool> mutation, string? replacementSourceKeyHash = null, bool invalidateSourceKey = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (replacementSourceKeyHash is not null) EnsureHash(replacementSourceKeyHash);
        var id = EnsureId(channelId);
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = await MutateAsync(channelId, mutation, transaction.Connection, transaction.Transaction, replacementSourceKeyHash, invalidateSourceKey, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return channel;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task AddAsync(
        OverlayChannel channel,
        string sourceKeyHash,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        EnsureHash(sourceKeyHash);
        return connection.ExecuteAsync(new CommandDefinition(
            InsertSql, Parameters(channel, sourceKeyHash), transaction: transaction, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<OverlayChannel?> MutateAsync(
        OverlayChannelId channelId,
        Func<OverlayChannel, bool> mutation,
        DbConnection connection,
        DbTransaction transaction,
        string? replacementSourceKeyHash = null,
        bool invalidateSourceKey = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (replacementSourceKeyHash is not null) EnsureHash(replacementSourceKeyHash);
        var id = EnsureId(channelId);
        var row = await connection.QuerySingleOrDefaultAsync<ChannelRow>(
                new CommandDefinition(GetForUpdateSql, new { Id = id }, transaction: transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var channel = Rehydrate(row);
        var changed = mutation(channel);
        var replaceKey = invalidateSourceKey || replacementSourceKeyHash is not null;
        if (changed || replaceKey)
        {
            var updated = await connection.ExecuteAsync(
                    new CommandDefinition(
                        UpdateSql,
                        Parameters(channel, replaceKey ? replacementSourceKeyHash : row.SourceKeyHash),
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            if (updated != 1) throw new InvalidOperationException("Der Overlay-Kanal konnte nicht eindeutig aktualisiert werden.");
        }

        return channel;
    }

    private static Guid EnsureId(OverlayChannelId id)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Die Overlay-Channel-ID darf nicht leer sein.", nameof(id));
        return id.Value;
    }

    private static object Parameters(OverlayChannel channel, string? sourceKeyHash) => new
    {
        Id = channel.Id.Value,
        channel.DisplayName,
        channel.Description,
        channel.IsEnabled,
        channel.IsArchived,
        channel.CreatedAtUtc,
        channel.UpdatedAtUtc,
        SourceKeyHash = sourceKeyHash
    };

    private static OverlayChannel Rehydrate(ChannelRow row) => OverlayChannel.Rehydrate(OverlayChannelId.Create(row.Id), row.DisplayName, row.Description, row.IsEnabled, row.IsArchived, row.CreatedAtUtc, row.UpdatedAtUtc);

    private static void EnsureHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character))) throw new ArgumentException("Der Source-Key-Hash muss ein SHA-256-Hexwert sein.", nameof(hash));
    }

    private sealed class ChannelRow
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsArchived { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public string? SourceKeyHash { get; set; }
    }
}
