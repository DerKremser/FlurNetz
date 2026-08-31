using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Inventory.Contracts;

/// <summary>
/// Stellt die neutrale Fähigkeit bereit, einer Community-Identität eine positive Item-Menge
/// innerhalb einer bereits bestehenden Datenbanktransaktion zu gewähren.
/// </summary>
/// <remarks>
/// Der Contract kennt weder Shop noch Rewards. Die fachliche Mengenlogik und der sparse
/// Inventory-Lifecycle bleiben Eigentum der Inventory-Implementierung.
/// </remarks>
public interface IInventoryQuantityGrant
{
    /// <summary>
    /// Gewährt eine positive Menge innerhalb der bereitgestellten Transaktion.
    /// </summary>
    Task GrantAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
