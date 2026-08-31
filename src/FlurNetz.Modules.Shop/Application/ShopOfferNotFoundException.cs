using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Kennzeichnet die atomare Mutation eines unbekannten Shop-Angebots.
/// </summary>
public sealed class ShopOfferNotFoundException : KeyNotFoundException
{
    /// <summary>
    /// Erstellt den fachlichen NotFound-Fehler für eine gültige Angebots-ID.
    /// </summary>
    public ShopOfferNotFoundException(ShopOfferId shopOfferId)
        : base($"Das Shop-Angebot '{shopOfferId.Value}' wurde nicht gefunden.")
    {
        if (shopOfferId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Der NotFound-Fehler benötigt eine nicht leere Shop-Angebots-ID.",
                nameof(shopOfferId));
        }

        ShopOfferId = shopOfferId;
    }

    /// <summary>
    /// Liefert die unbekannte Angebots-ID.
    /// </summary>
    public ShopOfferId ShopOfferId { get; }
}
