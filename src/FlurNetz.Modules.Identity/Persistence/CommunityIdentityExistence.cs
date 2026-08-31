using System.Data.Common;
using Dapper;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Identity.Persistence;

/// <summary>
/// Implementiert die schmale transaction-aware Existenzprüfung für Community-Identitäten.
/// </summary>
public sealed class CommunityIdentityExistence : ICommunityIdentityExistence
{
    private const string ExistsSql = """
        SELECT EXISTS
        (
            SELECT 1
            FROM community_identities
            WHERE id = @Id
        );
        """;

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        CommunityIdentityId communityIdentityId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var validCommunityIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        return await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    ExistsSql,
                    new { Id = validCommunityIdentityId.Value },
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
