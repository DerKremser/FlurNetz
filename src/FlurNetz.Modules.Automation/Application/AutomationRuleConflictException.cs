using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Bezeichnet eine nicht zulässige Management-Mutation.</summary>
public sealed class AutomationRuleConflictException : InvalidOperationException
{
    /// <summary>Erstellt den fachlichen Conflict-Fehler.</summary>
    public AutomationRuleConflictException(AutomationRuleId ruleId, string message)
        : base(message) => RuleId = ruleId;

    /// <summary>Betroffene Rule.</summary>
    public AutomationRuleId RuleId { get; }
}
