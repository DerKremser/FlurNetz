using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Inventory.Contracts;

namespace FlurNetz.Modules.Inventory.Application;

/// <summary>
/// Adapter der öffentlichen transaction-aware Inventory-Grant-Fähigkeit auf den bestehenden Store.
/// </summary>
public sealed class InventoryQuantityGrant : IInventoryQuantityGrant
{
    private readonly ICommunityInventoryStore store;

    /// <summary>
    /// Erstellt den Inventory-Grant-Adapter.
    /// </summary>
    public InventoryQuantityGrant(ICommunityInventoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc />
    public async Task GrantAsync(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        _ = await store.AddAsync(
                communityIdentityId,
                itemDefinitionId,
                amount,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
