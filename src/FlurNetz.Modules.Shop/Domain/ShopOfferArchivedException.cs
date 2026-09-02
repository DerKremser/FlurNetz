using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Domain;

/// <summary>
/// Signalisiert, dass ein endgültig archiviertes Shop-Angebot nicht reaktiviert werden darf.
/// </summary>
public sealed class ShopOfferArchivedException : InvalidOperationException
{
    /// <summary>
    /// Erstellt den fachlichen Konflikt für ein archiviertes Angebot.
    /// </summary>
    public ShopOfferArchivedException(ShopOfferId shopOfferId)
        : base($"Das Shop-Angebot '{ShopOfferId.Create(shopOfferId.Value).Value}' ist archiviert und kann nicht aktiviert werden.")
    {
        ShopOfferId = ShopOfferId.Create(shopOfferId.Value);
    }

    /// <summary>
    /// Liefert die archivierte Angebots-ID.
    /// </summary>
    public ShopOfferId ShopOfferId { get; }
}
