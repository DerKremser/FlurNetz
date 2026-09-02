using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Lädt alle Automation-Rules deterministisch sortiert.</summary>
public sealed class ListAutomationRules
{
    private readonly IAutomationRuleStore store;

    /// <summary>Erstellt den Use Case.</summary>
    public ListAutomationRules(IAutomationRuleStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Lädt alle Rules, einschließlich deaktivierter und archivierter.</summary>
    public Task<IReadOnlyList<AutomationRule>> ExecuteAsync(CancellationToken cancellationToken = default) => store.ListAsync(cancellationToken);
}
