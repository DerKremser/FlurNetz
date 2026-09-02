using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Eine einzelne, typisierte V1-Condition mit stabiler Position.
/// </summary>
public sealed record AutomationCondition
{
    /// <summary>Höchste zulässige Position.</summary>
    public const int MaximumPosition = 15;

    /// <summary>
    /// Erstellt und validiert eine Condition. Genau die zum Typ passende Wertspalte darf gesetzt sein.
    /// </summary>
    public AutomationCondition(
        int position,
        string conditionType,
        Guid? communityIdentityId = null,
        Guid? shopOfferId = null,
        Guid? itemDefinitionId = null,
        long? amount = null)
    {
        EnsurePosition(position);
        Position = position;
        ConditionType = EnsureType(conditionType);
        CommunityIdentityId = EnsureGuid(communityIdentityId, nameof(communityIdentityId));
        ShopOfferId = EnsureGuid(shopOfferId, nameof(shopOfferId));
        ItemDefinitionId = EnsureGuid(itemDefinitionId, nameof(itemDefinitionId));
        Amount = amount;
        EnsureValueShape(ConditionType, CommunityIdentityId, ShopOfferId, ItemDefinitionId, Amount);
    }

    /// <summary>Position innerhalb der Rule.</summary>
    public int Position { get; }

    /// <summary>Stabiler Condition-Typ.</summary>
    public string ConditionType { get; }

    /// <summary>Alias für API-/Mapping-Code.</summary>
    public string Type => ConditionType;

    /// <summary>Optionale Community-Identität für den Identity-Vergleich.</summary>
    public Guid? CommunityIdentityId { get; }

    /// <summary>Optionale Shop-Angebots-ID für den Angebotsvergleich.</summary>
    public Guid? ShopOfferId { get; }

    /// <summary>Optionale Item-Definition-ID für den Itemvergleich.</summary>
    public Guid? ItemDefinitionId { get; }

    /// <summary>Optionale Preisgrenze.</summary>
    public long? Amount { get; }

    /// <summary>Erstellt eine validierte Condition.</summary>
    public static AutomationCondition Create(int position, string conditionType, Guid? communityIdentityId = null, Guid? shopOfferId = null, Guid? itemDefinitionId = null, long? amount = null) =>
        new(position, conditionType, communityIdentityId, shopOfferId, itemDefinitionId, amount);

    /// <summary>Rehydriert eine persistierte Condition ohne stilles Reparieren.</summary>
    public static AutomationCondition Rehydrate(int position, string conditionType, Guid? communityIdentityId = null, Guid? shopOfferId = null, Guid? itemDefinitionId = null, long? amount = null) =>
        new(position, conditionType, communityIdentityId, shopOfferId, itemDefinitionId, amount);

    private static string EnsureType(string? conditionType)
    {
        if (conditionType is not (
            AutomationConditionTypes.CommunityIdentityEquals
            or AutomationConditionTypes.ShopOfferIdEquals
            or AutomationConditionTypes.ShopItemDefinitionIdEquals
            or AutomationConditionTypes.ShopPricePaidAtLeast
            or AutomationConditionTypes.ShopPricePaidAtMost))
        {
            throw new ArgumentException("Der Condition-Typ ist für Automation V1 nicht unterstützt.", nameof(conditionType));
        }

        return conditionType;
    }

    private static Guid? EnsureGuid(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Eine Condition-ID darf nicht leer sein.", parameterName);
        }

        return value;
    }

    private static void EnsurePosition(int position)
    {
        if (position is < 0 or > MaximumPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Condition-Positionen müssen zwischen 0 und 15 liegen.");
        }
    }

    private static void EnsureValueShape(string type, Guid? communityIdentityId, Guid? shopOfferId, Guid? itemDefinitionId, long? amount)
    {
        var valid = type switch
        {
            AutomationConditionTypes.CommunityIdentityEquals => communityIdentityId.HasValue && shopOfferId is null && itemDefinitionId is null && amount is null,
            AutomationConditionTypes.ShopOfferIdEquals => communityIdentityId is null && shopOfferId.HasValue && itemDefinitionId is null && amount is null,
            AutomationConditionTypes.ShopItemDefinitionIdEquals => communityIdentityId is null && shopOfferId is null && itemDefinitionId.HasValue && amount is null,
            AutomationConditionTypes.ShopPricePaidAtLeast or AutomationConditionTypes.ShopPricePaidAtMost => communityIdentityId is null && shopOfferId is null && itemDefinitionId is null && amount is >= 0,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException("Die Wertfelder der Condition passen nicht exakt zu ihrem Condition-Typ.");
        }
    }
}
