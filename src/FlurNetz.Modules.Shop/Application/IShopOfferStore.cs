using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Application;

/// <summary>
/// Definiert die interne Persistenzgrenze für den Shop-Angebotskatalog.
/// </summary>
/// <remarks>
/// Der Callback der atomaren Mutation ist synchron und darf ausschließlich Domain-Logik
/// ausführen. So kann während der offenen Datenbanktransaktion keine beliebige externe
/// asynchrone I/O eingeschleust werden.
/// </remarks>
public interface IShopOfferStore
{
    /// <summary>
    /// Persistiert ein bereits gültiges Shop-Angebot.
    /// </summary>
    Task AddAsync(
        ShopOffer offer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt ein Shop-Angebot oder liefert bei unbekannter ID <see langword="null"/>.
    /// </summary>
    Task<ShopOffer?> GetAsync(
        ShopOfferId shopOfferId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt alle Angebote in technisch deterministischer ID-Reihenfolge.
    /// </summary>
    Task<IReadOnlyList<ShopOffer>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt und mutiert ein Angebot atomar über einen synchronen Domain-Callback.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        ShopOfferId shopOfferId,
        Func<ShopOffer, TResult> operation,
        CancellationToken cancellationToken = default);
}
