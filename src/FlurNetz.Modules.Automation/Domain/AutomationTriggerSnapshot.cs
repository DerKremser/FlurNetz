namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Automation-eigener, unveränderlicher Snapshot eines Integration-Event-Triggers.
/// </summary>
public sealed record AutomationTriggerSnapshot
{
    /// <summary>Erstellt einen validierten Trigger-Snapshot.</summary>
    public AutomationTriggerSnapshot(
        Guid triggerMessageId,
        string messageType,
        int schemaVersion,
        DateTimeOffset occurredAtUtc,
        Guid communityIdentityId,
        Guid? shopPurchaseId = null,
        Guid? shopOfferId = null,
        Guid? itemDefinitionId = null,
        long? pricePaid = null,
        DateTimeOffset? purchasedAtUtc = null)
    {
        if (triggerMessageId == Guid.Empty || communityIdentityId == Guid.Empty)
        {
            throw new ArgumentException("Trigger- und Community-Identity-ID dürfen nicht leer sein.");
        }

        if (schemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Automation V1 unterstützt ausschließlich Schema-Version 1.");
        }

        if (messageType is not (AutomationTriggerTypes.EngagementMessageRecorded or AutomationTriggerTypes.ShopPurchaseCompleted))
        {
            throw new ArgumentException("Der Trigger-Typ ist für Automation V1 nicht unterstützt.", nameof(messageType));
        }

        EnsureUtcMicroseconds(occurredAtUtc, nameof(occurredAtUtc));
        EnsureOptionalGuid(shopPurchaseId, nameof(shopPurchaseId));
        EnsureOptionalGuid(shopOfferId, nameof(shopOfferId));
        EnsureOptionalGuid(itemDefinitionId, nameof(itemDefinitionId));
        if (pricePaid is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricePaid));
        }

        if (purchasedAtUtc.HasValue)
        {
            EnsureUtcMicroseconds(purchasedAtUtc.Value, nameof(purchasedAtUtc));
        }

        if (messageType == AutomationTriggerTypes.EngagementMessageRecorded
            && (shopPurchaseId.HasValue || shopOfferId.HasValue || itemDefinitionId.HasValue || pricePaid.HasValue || purchasedAtUtc.HasValue))
        {
            throw new ArgumentException("Der Engagement-Snapshot darf keine Shop-Werte enthalten.");
        }

        if (messageType == AutomationTriggerTypes.ShopPurchaseCompleted
            && (!shopPurchaseId.HasValue || !shopOfferId.HasValue || !itemDefinitionId.HasValue || !pricePaid.HasValue || !purchasedAtUtc.HasValue))
        {
            throw new ArgumentException("Der Shop-Snapshot muss alle Shop-Werte enthalten.");
        }

        TriggerMessageId = triggerMessageId;
        MessageType = messageType;
        SchemaVersion = schemaVersion;
        OccurredAtUtc = occurredAtUtc;
        CommunityIdentityId = communityIdentityId;
        ShopPurchaseId = shopPurchaseId;
        ShopOfferId = shopOfferId;
        ItemDefinitionId = itemDefinitionId;
        PricePaid = pricePaid;
        PurchasedAtUtc = purchasedAtUtc;
    }

    /// <summary>Nachrichten-ID des auslösenden Events.</summary>
    public Guid TriggerMessageId { get; }

    /// <summary>Logischer Nachrichten-/Trigger-Typ.</summary>
    public string TriggerType => MessageType;

    /// <summary>Logischer Nachrichten-Typ.</summary>
    public string MessageType { get; }

    /// <summary>Schema-Version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Entstehungszeitpunkt des Events.</summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>Community-Identität aus dem Event.</summary>
    public Guid CommunityIdentityId { get; }

    /// <summary>Shop-Purchase-ID für Shop-Events.</summary>
    public Guid? ShopPurchaseId { get; }

    /// <summary>Shop-Offer-ID für Shop-Events.</summary>
    public Guid? ShopOfferId { get; }

    /// <summary>Item-Definition-ID für Shop-Events.</summary>
    public Guid? ItemDefinitionId { get; }

    /// <summary>Bezahlter Preis für Shop-Events.</summary>
    public long? PricePaid { get; }

    /// <summary>Fachlicher Kaufzeitpunkt für Shop-Events.</summary>
    public DateTimeOffset? PurchasedAtUtc { get; }

    private static void EnsureOptionalGuid(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Eine Snapshot-ID darf nicht leer sein.", parameterName);
        }
    }

    private static void EnsureUtcMicroseconds(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException("Trigger-Zeitpunkte müssen in UTC und PostgreSQL-kompatibler Mikrosekundenpräzision vorliegen.", parameterName);
        }
    }
}
