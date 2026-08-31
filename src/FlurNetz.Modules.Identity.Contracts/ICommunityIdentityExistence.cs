using System.Data.Common;

namespace FlurNetz.Modules.Identity.Contracts;

/// <summary>
/// Stellt die neutrale Fähigkeit bereit, die Existenz einer internen Community-Identität
/// innerhalb einer bereits bestehenden Datenbanktransaktion zu prüfen.
/// </summary>
/// <remarks>
/// Der Contract kennt keinen aufrufenden Fachbereich. Verbindung und Transaktion werden
/// bewusst als ADO.NET-Basistypen übergeben, damit ein fremder fachlicher Slice seine eigene
/// atomare PostgreSQL-Grenze besitzen kann, ohne die Identity-Persistenz zu übernehmen.
/// </remarks>
public interface ICommunityIdentityExistence
{
    /// <summary>
    /// Prüft, ob die angegebene interne Identität innerhalb der bereitgestellten Transaktion existiert.
    /// </summary>
    Task<bool> ExistsAsync(
        CommunityIdentityId communityIdentityId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
