namespace FlurNetz.Modules.Automation.Domain;

/// <summary>Die in Automation V1 unterstützten Integration-Event-Trigger.</summary>
public static class AutomationTriggerTypes
{
    /// <summary>Engagement-Nachricht wurde aufgezeichnet.</summary>
    public const string EngagementMessageRecorded = "engagement.message-recorded";

    /// <summary>Shop-Kauf wurde abgeschlossen.</summary>
    public const string ShopPurchaseCompleted = "shop.purchase-completed";
}

/// <summary>Die in Automation V1 unterstützten Condition-Typen.</summary>
public static class AutomationConditionTypes
{
    /// <summary>Prüft die Community-Identität.</summary>
    public const string CommunityIdentityEquals = "community-identity.equals";

    /// <summary>Prüft die Shop-Angebots-ID.</summary>
    public const string ShopOfferIdEquals = "shop.offer-id.equals";

    /// <summary>Prüft die Item-Definition-ID.</summary>
    public const string ShopItemDefinitionIdEquals = "shop.item-definition-id.equals";

    /// <summary>Prüft einen Mindestpreis.</summary>
    public const string ShopPricePaidAtLeast = "shop.price-paid.at-least";

    /// <summary>Prüft einen Höchstpreis.</summary>
    public const string ShopPricePaidAtMost = "shop.price-paid.at-most";
}

/// <summary>Die in Automation V1 unterstützten Action-Typen.</summary>
public static class AutomationActionTypes
{
    /// <summary>Schreibt Economy gut.</summary>
    public const string EconomyCredit = "economy.credit";

    /// <summary>Erzeugt eine Notification.</summary>
    public const string NotificationCreate = "notification.create";
}
