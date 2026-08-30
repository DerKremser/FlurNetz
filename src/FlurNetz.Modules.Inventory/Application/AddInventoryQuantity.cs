using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Domain;

namespace FlurNetz.Modules.Inventory.Application;

/// <summary>
/// Fügt über die atomare Persistenzgrenze des Inventory-Moduls eine positive Menge hinzu.
/// </summary>
/// <remarks>
/// Der Use Case enthält keine SQL- oder Transaktionslogik. Die fachliche Mengenvalidierung
/// bleibt in <see cref="CommunityInventoryEntry.Add(long)"/>; der Store hält den gesamten
/// Persistenzvorgang atomar.
/// </remarks>
public sealed class AddInventoryQuantity
{
    private readonly ICommunityInventoryStore store;

    /// <summary>
    /// Erstellt den Use Case mit der modulbezogenen Persistenzgrenze.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Bestandspositionen.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public AddInventoryQuantity(ICommunityInventoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Fügt eine positive Menge hinzu und liefert den neuen Bestand.
    /// </summary>
    public Task<InventoryQuantity> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        return store.AddAsync(
            communityIdentityId,
            itemDefinitionId,
            amount,
            cancellationToken);
    }
}
