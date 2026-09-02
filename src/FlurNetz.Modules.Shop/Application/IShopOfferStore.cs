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
    /// Lädt alle Angebote in der fachlichen Katalogreihenfolge SortOrder aufsteigend und
    /// ShopOfferId aufsteigend als deterministischem Tie-Breaker.
    /// </summary>
    Task<IReadOnlyList<ShopOffer>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt und mutiert ein Angebot atomar über einen synchronen Domain-Callback.
    /// </summary>
    /// <remarks>
    /// Die nicht-generische Signatur akzeptiert ausschließlich <see cref="Func{T, TResult}"/>
    /// mit dem Ergebnis <see cref="bool"/> und kann daher keine asynchrone Callback-Rückgabe
    /// wie <c>Task</c> oder <c>Task&lt;T&gt;</c> aufnehmen.
    /// </remarks>
    Task<bool> ExecuteAsync(
        ShopOfferId shopOfferId,
        Func<ShopOffer, bool> operation,
        CancellationToken cancellationToken = default);
}
