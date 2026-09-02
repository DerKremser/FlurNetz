using System.Data.Common;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>
/// Transaction-aware Runtime-Persistenzgrenze der Automation-Engine.
/// </summary>
public interface IAutomationRuntimeStore
{
    /// <summary> Lädt aktive Rules unter stabiler PostgreSQL-Shared-Lock-Grenze. </summary>
    Task<IReadOnlyList<AutomationRule>> LoadActiveRulesAsync(
        string triggerType,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Reserviert eine Execution idempotent.</summary>
    Task<bool> ReserveExecutionAsync(
        AutomationExecution execution,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
