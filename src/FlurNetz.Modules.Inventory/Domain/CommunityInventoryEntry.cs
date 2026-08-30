using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Inventory.Domain;

/// <summary>
/// Hält den Mengenbestand genau einer Item-Definition für genau eine Community-Identität.
/// </summary>
/// <remarks>
/// Die Kombination aus <see cref="CommunityIdentityId"/> und <see cref="ItemDefinitionId"/>
/// bezeichnet die fachliche Bestandsposition. Neue Positionen starten bei null. Herkunft,
/// Preise, Rewards, Käufe und Plattformidentitäten sind keine Verantwortung von Inventory.
/// </remarks>
public sealed class CommunityInventoryEntry
{
    private CommunityInventoryEntry(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId)
    {
        CommunityIdentityId = communityIdentityId;
        ItemDefinitionId = itemDefinitionId;
        Quantity = InventoryQuantity.Zero;
    }

    private CommunityInventoryEntry(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        InventoryQuantity quantity)
    {
        CommunityIdentityId = communityIdentityId;
        ItemDefinitionId = itemDefinitionId;
        Quantity = quantity;
    }

    /// <summary>
    /// Liefert die unveränderliche interne Community-Identität, der der Bestand gehört.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert die unveränderliche Item-Definition, deren Bestand geführt wird.
    /// </summary>
    public ItemDefinitionId ItemDefinitionId { get; }

    /// <summary>
    /// Liefert die aktuelle nicht-negative Menge.
    /// </summary>
    public InventoryQuantity Quantity { get; private set; }

    /// <summary>
    /// Erzeugt eine neue Bestandsposition mit der Anfangsmenge null.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <param name="itemDefinitionId">Die gültige Item-Definition-ID.</param>
    /// <returns>Eine neue Inventory-Bestandsposition.</returns>
    /// <exception cref="ArgumentException">
    /// Wenn <paramref name="communityIdentityId"/> oder <paramref name="itemDefinitionId"/> leer ist.
    /// </exception>
    public static CommunityInventoryEntry Create(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId)
    {
        EnsureValidCommunityIdentityId(communityIdentityId);
        EnsureValidItemDefinitionId(itemDefinitionId);

        return new CommunityInventoryEntry(communityIdentityId, itemDefinitionId);
    }

    /// <summary>
    /// Rekonstruiert eine bereits persistierte Bestandsposition ohne fachliche Neuanlage.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <param name="itemDefinitionId">Die gültige Item-Definition-ID.</param>
    /// <param name="quantity">Die bereits validierte gespeicherte Menge.</param>
    /// <returns>Die exakt rekonstruierte Inventory-Bestandsposition.</returns>
    /// <exception cref="ArgumentException">
    /// Wenn <paramref name="communityIdentityId"/> oder <paramref name="itemDefinitionId"/> leer ist.
    /// </exception>
    public static CommunityInventoryEntry Rehydrate(
        CommunityIdentityId communityIdentityId,
        ItemDefinitionId itemDefinitionId,
        InventoryQuantity quantity)
    {
        EnsureValidCommunityIdentityId(communityIdentityId);
        EnsureValidItemDefinitionId(itemDefinitionId);

        return new CommunityInventoryEntry(communityIdentityId, itemDefinitionId, quantity);
    }

    /// <summary>
    /// Erhöht den Bestand um eine positive Anzahl.
    /// </summary>
    /// <param name="amount">Die hinzuzufügende positive Anzahl.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="OverflowException">Wenn die Erhöhung die technische Obergrenze überschreiten würde.</exception>
    public void Add(long amount)
    {
        Quantity = Quantity.Add(amount);
    }

    /// <summary>
    /// Verringert den Bestand um eine positive Anzahl ohne Unterbestand.
    /// </summary>
    /// <param name="amount">Die zu entnehmende positive Anzahl.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="InsufficientInventoryQuantityException">Wenn der Bestand für die Entnahme nicht ausreicht.</exception>
    public void Remove(long amount)
    {
        Quantity = Quantity.Remove(amount);
    }

    private static void EnsureValidCommunityIdentityId(CommunityIdentityId communityIdentityId)
    {
        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Inventory-Bestandsposition benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }
    }

    private static void EnsureValidItemDefinitionId(ItemDefinitionId itemDefinitionId)
    {
        if (itemDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Inventory-Bestandsposition benötigt eine nicht leere Item-Definition-ID.",
                nameof(itemDefinitionId));
        }
    }
}
