namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Wird ausgelöst, wenn eine archivierte Rule aktiviert oder ersetzt werden soll.
/// </summary>
public sealed class AutomationRuleArchivedException : InvalidOperationException
{
    /// <summary>Erstellt den fachlichen Archiv-Konflikt.</summary>
    public AutomationRuleArchivedException(AutomationRuleId ruleId)
        : base($"Die Automation-Rule '{ruleId.Value}' ist archiviert und kann nicht mehr verändert werden.")
    {
        RuleId = ruleId;
    }

    /// <summary>Die betroffene Rule.</summary>
    public AutomationRuleId RuleId { get; }
}
