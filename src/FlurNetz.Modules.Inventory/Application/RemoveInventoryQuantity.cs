using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Domain;

namespace FlurNetz.Modules.Inventory.Application;

/// <summary>
/// Entfernt über die atomare Persistenzgrenze des Inventory-Moduls eine positive Menge.
/// </summary>
/// <remarks>
/// Der Use Case enthält keine SQL- oder Transaktionslogik. Unterbestand und ungültige
/// Mengenänderungen bleiben fachliche Verantwortung der Inventory-Domain.
/// </remarks>
public sealed class RemoveInventoryQuantity
{
    private readonly ICommunityInventoryStore store;

    /// <summary>
    /// Erstellt den Use Case mit der modulbezogenen Persistenzgrenze.
    /// </summary>
    /// <param name="store">Atomarer Store für Community-Bestandspositionen.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public RemoveInventoryQuantity(ICommunityInventoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Entfernt eine positive Menge und liefert den verbleibenden Bestand.
    /// </summary>
    public Task<InventoryQuantity> ExecuteAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        CancellationToken cancellationToken = default)
    {
        return store.RemoveAsync(
            communityIdentityId,
            itemDefinitionId,
            amount,
            cancellationToken);
    }
}
