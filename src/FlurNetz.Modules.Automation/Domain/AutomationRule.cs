namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Persistiertes Automation-V1-Aggregat mit strengen Rule-, Condition- und Action-Invarianten.
/// </summary>
public sealed class AutomationRule
{
    /// <summary>Maximale Länge des Anzeigenamens.</summary>
    public const int MaxDisplayNameLength = 100;

    /// <summary>Maximale Länge der optionalen Beschreibung.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>Maximale Zahl der Conditions.</summary>
    public const int MaximumConditions = 16;

    /// <summary>Maximale Zahl der Actions.</summary>
    public const int MaximumActions = 16;

    private AutomationRule(
        AutomationRuleId id,
        string displayName,
        string? description,
        string triggerType,
        IReadOnlyList<AutomationCondition> conditions,
        IReadOnlyList<AutomationAction> actions,
        int sortOrder,
        bool isEnabled,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        AutomationRuleId = id;
        DisplayName = displayName;
        Description = description;
        TriggerType = triggerType;
        Conditions = conditions;
        Actions = actions;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        IsArchived = isArchived;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Stabile Rule-ID.</summary>
    public AutomationRuleId AutomationRuleId { get; }

    /// <summary>Alias für Adapter, die eine kurze ID erwarten.</summary>
    public AutomationRuleId Id => AutomationRuleId;

    /// <summary>Alias für fachliche Aufrufer.</summary>
    public AutomationRuleId RuleId => AutomationRuleId;

    /// <summary>Kanonischer Anzeigename.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Kanonische optionale Beschreibung.</summary>
    public string? Description { get; private set; }

    /// <summary>Stabiler Trigger-Typ.</summary>
    public string TriggerType { get; private set; }

    /// <summary>AND-verknüpfte Conditions in stabiler Position.</summary>
    public IReadOnlyList<AutomationCondition> Conditions { get; private set; }

    /// <summary>Actions in stabiler, lückenloser Ausführungsposition.</summary>
    public IReadOnlyList<AutomationAction> Actions { get; private set; }

    /// <summary>Nicht-negative deterministische Sortierung.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gibt an, ob die Rule zur Laufzeit aktiv ist.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Gibt an, ob die Rule terminal archiviert ist.</summary>
    public bool IsArchived { get; private set; }

    /// <summary>Zeitpunkt der Erstellung.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Zeitpunkt der letzten tatsächlichen Änderung.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Erstellt eine neue deaktivierte, nicht archivierte Rule.</summary>
    public static AutomationRule Create(
        AutomationRuleId id,
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        DateTimeOffset createdAtUtc) =>
        Create(id, displayName, description, triggerType, conditions, actions, 0, createdAtUtc);

    /// <summary>Erstellt eine neue Rule mit expliziter Sortierung und Zeit.</summary>
    public static AutomationRule Create(
        AutomationRuleId id,
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        int sortOrder,
        DateTimeOffset createdAtUtc)
    {
        EnsureId(id);
        var normalizedDisplayName = AutomationText.Required(displayName, nameof(displayName), "Der Automation-Anzeigename", MaxDisplayNameLength);
        var normalizedDescription = AutomationText.Optional(description, nameof(description), "Die Automation-Beschreibung", MaxDescriptionLength);
        var normalizedTrigger = EnsureTriggerType(triggerType);
        var validConditions = ValidateConditions(conditions, normalizedTrigger);
        var validActions = ValidateActions(actions);
        EnsureSortOrder(sortOrder);
        var timestamp = EnsureTimestamp(createdAtUtc, nameof(createdAtUtc));

        return new AutomationRule(
            id,
            normalizedDisplayName,
            normalizedDescription,
            normalizedTrigger,
            validConditions,
            validActions,
            sortOrder,
            isEnabled: false,
            isArchived: false,
            timestamp,
            timestamp);
    }

    /// <summary>Rehydriert eine persistierte Rule und validiert auch beschädigte Zustände.</summary>
    public static AutomationRule Rehydrate(
        AutomationRuleId id,
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        int sortOrder,
        bool isEnabled,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureId(id);
        AutomationText.EnsureCanonical(displayName, nameof(displayName), "der Automation-Anzeigename", MaxDisplayNameLength, allowNull: false);
        AutomationText.EnsureCanonical(description, nameof(description), "die Automation-Beschreibung", MaxDescriptionLength, allowNull: true);
        var normalizedTrigger = EnsureTriggerType(triggerType);
        var validConditions = ValidateConditions(conditions, normalizedTrigger);
        var validActions = ValidateActions(actions);
        EnsureSortOrder(sortOrder);
        if (isArchived && isEnabled)
        {
            throw new ArgumentException("Eine archivierte Automation-Rule darf nicht aktiviert sein.", nameof(isArchived));
        }

        var created = EnsureTimestamp(createdAtUtc, nameof(createdAtUtc));
        var updated = EnsureTimestamp(updatedAtUtc, nameof(updatedAtUtc));
        if (updated < created)
        {
            throw new ArgumentException("UpdatedAtUtc darf nicht vor CreatedAtUtc liegen.", nameof(updatedAtUtc));
        }

        return new AutomationRule(
            id,
            displayName,
            description,
            normalizedTrigger,
            validConditions,
            validActions,
            sortOrder,
            isEnabled,
            isArchived,
            created,
            updated);
    }

    /// <summary>Aktiviert die Rule mit einem vorgegebenen Änderungszeitpunkt.</summary>
    public bool Enable(DateTimeOffset updatedAtUtc)
    {
        if (IsArchived)
        {
            throw new AutomationRuleArchivedException(AutomationRuleId);
        }

        if (IsEnabled)
        {
            return false;
        }

        IsEnabled = true;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Deaktiviert die Rule mit einem vorgegebenen Änderungszeitpunkt.</summary>
    public bool Disable(DateTimeOffset updatedAtUtc)
    {
        if (IsArchived || !IsEnabled)
        {
            return false;
        }

        IsEnabled = false;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Archiviert terminal mit einem vorgegebenen Änderungszeitpunkt.</summary>
    public bool Archive(DateTimeOffset updatedAtUtc)
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        IsEnabled = false;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Ersetzt die vollständige Konfiguration atomar ohne künstlichen No-op-Zeitstempel.</summary>
    public bool ReplaceConfiguration(
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        int sortOrder,
        DateTimeOffset updatedAtUtc)
    {
        if (IsArchived || IsEnabled)
        {
            throw new InvalidOperationException("Nur deaktivierte, nicht archivierte Automation-Rules dürfen ersetzt werden.");
        }

        var normalizedDisplayName = AutomationText.Required(displayName, nameof(displayName), "Der Automation-Anzeigename", MaxDisplayNameLength);
        var normalizedDescription = AutomationText.Optional(description, nameof(description), "Die Automation-Beschreibung", MaxDescriptionLength);
        var normalizedTrigger = EnsureTriggerType(triggerType);
        var validConditions = ValidateConditions(conditions, normalizedTrigger);
        var validActions = ValidateActions(actions);
        EnsureSortOrder(sortOrder);

        var changed = !string.Equals(DisplayName, normalizedDisplayName, StringComparison.Ordinal)
            || !string.Equals(Description, normalizedDescription, StringComparison.Ordinal)
            || !string.Equals(TriggerType, normalizedTrigger, StringComparison.Ordinal)
            || SortOrder != sortOrder
            || !Conditions.SequenceEqual(validConditions)
            || !Actions.SequenceEqual(validActions);
        if (!changed)
        {
            return false;
        }

        DisplayName = normalizedDisplayName;
        Description = normalizedDescription;
        TriggerType = normalizedTrigger;
        Conditions = validConditions;
        Actions = validActions;
        SortOrder = sortOrder;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Prüft die AND-Conditions gegen einen Automation-eigenen Snapshot.</summary>
    public bool Matches(AutomationTriggerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(TriggerType, snapshot.TriggerType, StringComparison.Ordinal))
        {
            return false;
        }

        return Conditions.All(condition => condition.ConditionType switch
        {
            AutomationConditionTypes.CommunityIdentityEquals => snapshot.CommunityIdentityId == condition.CommunityIdentityId,
            AutomationConditionTypes.ShopOfferIdEquals => snapshot.ShopOfferId == condition.ShopOfferId,
            AutomationConditionTypes.ShopItemDefinitionIdEquals => snapshot.ItemDefinitionId == condition.ItemDefinitionId,
            AutomationConditionTypes.ShopPricePaidAtLeast => snapshot.PricePaid is long price && price >= condition.Amount,
            AutomationConditionTypes.ShopPricePaidAtMost => snapshot.PricePaid is long price && price <= condition.Amount,
            _ => false
        });
    }

    private static IReadOnlyList<AutomationCondition> ValidateConditions(IEnumerable<AutomationCondition> conditions, string triggerType)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        var values = conditions.ToArray();
        if (values.Length > MaximumConditions)
        {
            throw new ArgumentException($"Eine Automation-Rule darf höchstens {MaximumConditions} Conditions enthalten.", nameof(conditions));
        }

        if (values.Any(condition => condition is null))
        {
            throw new ArgumentException("Eine Condition darf nicht null sein.", nameof(conditions));
        }

        EnsureContiguousPositions(values.Select(condition => condition.Position), "Conditions");
        if (values.GroupBy(condition => condition.ConditionType, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Jeder Condition-Typ darf pro Rule höchstens einmal vorkommen.", nameof(conditions));
        }

        foreach (var condition in values)
        {
            var compatible = triggerType == AutomationTriggerTypes.ShopPurchaseCompleted
                || condition.ConditionType == AutomationConditionTypes.CommunityIdentityEquals;
            if (!compatible)
            {
                throw new ArgumentException("Die Condition ist mit dem gewählten Trigger nicht kompatibel.", nameof(conditions));
            }
        }

        var minimum = values.SingleOrDefault(condition => condition.ConditionType == AutomationConditionTypes.ShopPricePaidAtLeast)?.Amount;
        var maximum = values.SingleOrDefault(condition => condition.ConditionType == AutomationConditionTypes.ShopPricePaidAtMost)?.Amount;
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            throw new ArgumentException("Die minimale Preisgrenze darf nicht über der maximalen Preisgrenze liegen.", nameof(conditions));
        }

        return Array.AsReadOnly(values);
    }

    private static IReadOnlyList<AutomationAction> ValidateActions(IEnumerable<AutomationAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var values = actions.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("Eine Automation-Rule benötigt mindestens eine Action.", nameof(actions));
        }

        if (values.Length > MaximumActions)
        {
            throw new ArgumentException($"Eine Automation-Rule darf höchstens {MaximumActions} Actions enthalten.", nameof(actions));
        }

        if (values.Any(action => action is null))
        {
            throw new ArgumentException("Eine Action darf nicht null sein.", nameof(actions));
        }

        EnsureContiguousPositions(values.Select(action => action.Position), "Actions");
        return Array.AsReadOnly(values);
    }

    private static void EnsureContiguousPositions(IEnumerable<int> positions, string fieldName)
    {
        var ordered = positions.Order().ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index] != index)
            {
                throw new ArgumentException($"{fieldName}-Positionen müssen lückenlos bei 0 beginnen.", fieldName);
            }
        }
    }

    private static string EnsureTriggerType(string? triggerType)
    {
        if (triggerType is not (AutomationTriggerTypes.EngagementMessageRecorded or AutomationTriggerTypes.ShopPurchaseCompleted))
        {
            throw new ArgumentException("Der Trigger-Typ ist für Automation V1 nicht unterstützt.", nameof(triggerType));
        }

        return triggerType;
    }

    private static void EnsureId(AutomationRuleId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Eine Automation-Rule benötigt eine nicht leere ID.", nameof(id));
        }
    }

    private static void EnsureSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Die Sortierreihenfolge muss größer oder gleich null sein.");
        }
    }

    private static DateTimeOffset EnsureTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException("Automation-Zeitpunkte müssen in UTC und PostgreSQL-kompatibler Mikrosekundenpräzision vorliegen.", parameterName);
        }

        return value;
    }

    private void SetUpdatedAt(DateTimeOffset value)
    {
        var timestamp = EnsureTimestamp(value, nameof(value));
        if (timestamp < CreatedAtUtc)
        {
            throw new ArgumentException("UpdatedAtUtc darf nicht vor CreatedAtUtc liegen.", nameof(value));
        }

        UpdatedAtUtc = timestamp;
    }

}
