using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Bezeichnet eine unbekannte Automation-Rule.</summary>
public sealed class AutomationRuleNotFoundException : KeyNotFoundException
{
    /// <summary>Erstellt den NotFound-Fehler.</summary>
    public AutomationRuleNotFoundException(AutomationRuleId ruleId)
        : base($"Die Automation-Rule '{ruleId.Value}' wurde nicht gefunden.") => RuleId = ruleId;

    /// <summary>Gesuchte Rule.</summary>
    public AutomationRuleId RuleId { get; }
}
