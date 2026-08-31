using FlurNetz.Messaging.Integration;

namespace FlurNetz.Modules.Shop.Contracts;

/// <summary>
/// Bezeichnet die fachliche Tatsache eines erfolgreich abgeschlossenen Shop-Kaufs.
/// </summary>
/// <remarks>
/// Die Payload enthält ausschließlich den unveränderlichen historischen Kauf. Die Request-ID
/// gehört zur Idempotenzgrenze des Commands und wird deshalb nicht als fachlicher Eventinhalt
/// dupliziert.
/// </remarks>
public sealed record ShopPurchaseCompletedIntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Stabiler logischer Nachrichtentyp.
    /// </summary>
    public const string MessageType = "shop.purchase-completed";

    /// <summary>
    /// Version des stabilen Payload-Schemas.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// Erstellt das Integration Event für einen abgeschlossenen Kauf.
    /// </summary>
    public ShopPurchaseCompletedIntegrationEvent(
        Guid shopPurchaseId,
        Guid shopOfferId,
        Guid communityIdentityId,
        Guid itemDefinitionId,
        long pricePaid,
        DateTimeOffset purchasedAtUtc)
    {
        EnsureNotEmpty(shopPurchaseId, nameof(shopPurchaseId));
        EnsureNotEmpty(shopOfferId, nameof(shopOfferId));
        EnsureNotEmpty(communityIdentityId, nameof(communityIdentityId));
        EnsureNotEmpty(itemDefinitionId, nameof(itemDefinitionId));

        if (pricePaid < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricePaid), pricePaid, "Der bezahlte Preis darf nicht negativ sein.");
        }

        if (purchasedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Der Kaufzeitpunkt des Events muss in UTC vorliegen.", nameof(purchasedAtUtc));
        }

        if (purchasedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Der Kaufzeitpunkt des Events muss PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                nameof(purchasedAtUtc));
        }

        ShopPurchaseId = shopPurchaseId;
        ShopOfferId = shopOfferId;
        CommunityIdentityId = communityIdentityId;
        ItemDefinitionId = itemDefinitionId;
        PricePaid = pricePaid;
        PurchasedAtUtc = purchasedAtUtc;
    }

    public Guid ShopPurchaseId { get; }

    public Guid ShopOfferId { get; }

    public Guid CommunityIdentityId { get; }

    public Guid ItemDefinitionId { get; }

    public long PricePaid { get; }

    public DateTimeOffset PurchasedAtUtc { get; }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Event-IDs dürfen nicht leer sein.", parameterName);
        }
    }
}
