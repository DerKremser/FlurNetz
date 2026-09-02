using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Read-only-Persistenzgrenze für Execution-History.</summary>
public interface IAutomationExecutionHistoryStore
{
    /// <summary>Lädt eine Rule-History-Seite nach Keyset-Cursor.</summary>
    Task<IReadOnlyList<AutomationExecution>> ListAsync(
        AutomationRuleId ruleId,
        AutomationExecutionCursor? cursor,
        int take,
        CancellationToken cancellationToken = default);
}
