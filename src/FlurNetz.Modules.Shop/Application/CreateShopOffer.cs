using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Erzeugt und persistiert ein neues, zunächst deaktiviertes Shop-Angebot.
/// </summary>
public sealed class CreateShopOffer
{
    private readonly IShopOfferStore store;

    /// <summary>
    /// Erstellt den Katalog-Create-Use-Case.
    /// </summary>
    public CreateShopOffer(IShopOfferStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Vergibt die Angebots-ID serverseitig, validiert die Domain und persistiert das Angebot.
    /// </summary>
    public async Task<ShopOffer> ExecuteAsync(
        ItemDefinitionId itemDefinitionId,
        string displayName,
        string? description = null,
        ShopPrice price = default,
        AvailabilityWindow availabilityWindow = default,
        int? purchaseLimitPerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            itemDefinitionId,
            displayName,
            description,
            price,
            availabilityWindow,
            purchaseLimitPerIdentity);

        await store.AddAsync(offer, cancellationToken).ConfigureAwait(false);
        return offer;
    }
}
