using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Domain;

namespace FlurNetz.Modules.Inventory.Application;

/// <summary>
/// Definiert die modulinterne Persistenzgrenze für mengenbasierte Community-Bestände.
/// </summary>
/// <remarks>
/// Add und Remove sind vollständige atomare Read/Modify/Write-Operationen. Die Domain wird
/// innerhalb derselben Datenbanktransaktion rehydriert und mutiert, damit parallele Änderungen
/// an derselben Bestandsposition keine fachlichen Updates verlieren.
/// </remarks>
public interface ICommunityInventoryStore
{
    /// <summary>
    /// Fügt einer Bestandsposition atomar eine positive Menge hinzu.
    /// </summary>
    Task<InventoryQuantity> AddAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fügt einer Bestandsposition innerhalb einer bereits bestehenden Transaktion eine
    /// positive Menge hinzu.
    /// </summary>
    /// <remarks>
    /// Dieser Overload führt keinen Commit aus und verwendet denselben Domain- und
    /// Sparse-Lifecycle wie der normale Add-Pfad.
    /// </remarks>
    Task<InventoryQuantity> AddAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt atomar eine positive Menge ohne Unterbestand.
    /// </summary>
    /// <exception cref="InsufficientInventoryQuantityException">Wenn der vorhandene Bestand nicht ausreicht.</exception>
    Task<InventoryQuantity> RemoveAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt genau eine Bestandsposition, ohne beim Lesen einen fehlenden Zustand anzulegen.
    /// </summary>
    Task<CommunityInventoryEntry?> GetAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        CancellationToken cancellationToken = default);
}
