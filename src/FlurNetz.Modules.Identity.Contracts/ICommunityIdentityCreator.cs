using System.Data.Common;

namespace FlurNetz.Modules.Identity.Contracts;

/// <summary>
/// Öffentliche Identity-Owner-Fähigkeit zum Erzeugen einer neuen internen Community-Identity.
/// </summary>
/// <remarks>
/// Die ADO.NET-Transaktionsparameter erlauben einem Composition-Root, Identity zusammen mit
/// einem eigenen fachlichen Vorgang atomar zu erzeugen, ohne Identity-SQL zu übernehmen.
/// </remarks>
public interface ICommunityIdentityCreator
{
    Task<CommunityIdentityId> CreateAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
