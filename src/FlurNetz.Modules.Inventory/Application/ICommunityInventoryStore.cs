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
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="itemDefinitionId">Die Inventory-eigene Item-Definition.</param>
    /// <param name="amount">Die positive hinzuzufügende Menge.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Die neue Menge nach erfolgreichem Commit.</returns>
    Task<InventoryQuantity> AddAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt atomar eine positive Menge ohne Unterbestand.
    /// </summary>
    /// <param name="communityIdentityId">Die bereits aufgelöste interne Identität.</param>
    /// <param name="itemDefinitionId">Die Inventory-eigene Item-Definition.</param>
    /// <param name="amount">Die positive zu entfernende Menge.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Vorgangs.</param>
    /// <returns>Die neue Menge nach erfolgreichem Commit.</returns>
    /// <exception cref="InsufficientInventoryQuantityException">Wenn der vorhandene Bestand nicht ausreicht.</exception>
    Task<InventoryQuantity> RemoveAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt genau eine Bestandsposition, ohne beim Lesen einen fehlenden Zustand anzulegen.
    /// </summary>
    /// <param name="communityIdentityId">Die gesuchte interne Identität.</param>
    /// <param name="itemDefinitionId">Die gesuchte Item-Definition.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Lesevorgangs.</param>
    /// <returns>Die Bestandsposition oder <see langword="null"/>, wenn keine positive Position persistiert ist.</returns>
    Task<CommunityInventoryEntry?> GetAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        CancellationToken cancellationToken = default);
}
