using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Lädt eine einzelne Automation-Rule.</summary>
public sealed class GetAutomationRule
{
    private readonly IAutomationRuleStore store;

    /// <summary>Erstellt den Use Case.</summary>
    public GetAutomationRule(IAutomationRuleStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Lädt eine Rule oder liefert null.</summary>
    public Task<AutomationRule?> ExecuteAsync(AutomationRuleId ruleId, CancellationToken cancellationToken = default) =>
        store.GetAsync(AutomationRuleId.Create(ruleId.Value), cancellationToken);
}
