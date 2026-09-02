namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Persistenter Snapshot eines erfolgreich reservierten und commiteten Rule-Laufs.
/// </summary>
public sealed class AutomationExecution
{
    private AutomationExecution(
        AutomationExecutionId id,
        AutomationRuleId ruleId,
        Guid triggerMessageId,
        string triggerMessageType,
        int triggerSchemaVersion,
        Guid communityIdentityId,
        DateTimeOffset triggerOccurredAtUtc,
        DateTimeOffset executedAtUtc)
    {
        Id = id;
        AutomationRuleId = ruleId;
        TriggerMessageId = triggerMessageId;
        TriggerMessageType = triggerMessageType;
        TriggerSchemaVersion = triggerSchemaVersion;
        CommunityIdentityId = communityIdentityId;
        TriggerOccurredAtUtc = triggerOccurredAtUtc;
        ExecutedAtUtc = executedAtUtc;
    }

    /// <summary>Execution-ID.</summary>
    public AutomationExecutionId Id { get; }

    /// <summary>Alias für die explizite Snapshot-Bezeichnung.</summary>
    public AutomationExecutionId AutomationExecutionId => Id;

    /// <summary>Rule-ID.</summary>
    public AutomationRuleId AutomationRuleId { get; }

    /// <summary>Trigger-Nachrichten-ID.</summary>
    public Guid TriggerMessageId { get; }

    /// <summary>Trigger-Nachrichtentyp.</summary>
    public string TriggerMessageType { get; }

    /// <summary>Trigger-Schema-Version.</summary>
    public int TriggerSchemaVersion { get; }

    /// <summary>Community-Identität des Triggers.</summary>
    public Guid CommunityIdentityId { get; }

    /// <summary>Originaler Trigger-Zeitpunkt.</summary>
    public DateTimeOffset TriggerOccurredAtUtc { get; }

    /// <summary>Zeitpunkt der Execution-Reservation.</summary>
    public DateTimeOffset ExecutedAtUtc { get; }

    /// <summary>Erstellt einen Execution-Snapshot aus einem Trigger-Snapshot.</summary>
    public static AutomationExecution Create(AutomationExecutionId id, AutomationRuleId ruleId, AutomationTriggerSnapshot snapshot, DateTimeOffset executedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CreateCore(id, ruleId, snapshot.TriggerMessageId, snapshot.MessageType, snapshot.SchemaVersion, snapshot.CommunityIdentityId, snapshot.OccurredAtUtc, executedAtUtc);
    }

    /// <summary>Rehydriert einen Execution-Snapshot.</summary>
    public static AutomationExecution Rehydrate(AutomationExecutionId id, AutomationRuleId ruleId, Guid triggerMessageId, string triggerMessageType, int triggerSchemaVersion, Guid communityIdentityId, DateTimeOffset triggerOccurredAtUtc, DateTimeOffset executedAtUtc) =>
        CreateCore(id, ruleId, triggerMessageId, triggerMessageType, triggerSchemaVersion, communityIdentityId, triggerOccurredAtUtc, executedAtUtc);

    private static AutomationExecution CreateCore(AutomationExecutionId id, AutomationRuleId ruleId, Guid triggerMessageId, string triggerMessageType, int triggerSchemaVersion, Guid communityIdentityId, DateTimeOffset triggerOccurredAtUtc, DateTimeOffset executedAtUtc)
    {
        if (id.Value == Guid.Empty || ruleId.Value == Guid.Empty || triggerMessageId == Guid.Empty || communityIdentityId == Guid.Empty)
        {
            throw new ArgumentException("Execution-, Rule-, Trigger- und Community-Identity-IDs dürfen nicht leer sein.");
        }

        if (triggerMessageType is not (AutomationTriggerTypes.EngagementMessageRecorded or AutomationTriggerTypes.ShopPurchaseCompleted))
        {
            throw new ArgumentException("Der Trigger-Typ der Execution ist für Automation V1 nicht unterstützt.", nameof(triggerMessageType));
        }

        if (triggerSchemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(triggerSchemaVersion), triggerSchemaVersion, "Automation V1 unterstützt ausschließlich Schema-Version 1.");
        }

        EnsureTimestamp(triggerOccurredAtUtc, nameof(triggerOccurredAtUtc));
        EnsureTimestamp(executedAtUtc, nameof(executedAtUtc));
        return new AutomationExecution(id, ruleId, triggerMessageId, triggerMessageType, triggerSchemaVersion, communityIdentityId, triggerOccurredAtUtc, executedAtUtc);
    }

    private static void EnsureTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException("Execution-Zeitpunkte müssen in UTC und PostgreSQL-kompatibler Mikrosekundenpräzision vorliegen.", parameterName);
        }
    }
}
