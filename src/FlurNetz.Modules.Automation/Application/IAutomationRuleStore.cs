using System.Data.Common;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>
/// Management-Persistenzgrenze für Automation-Rules.
/// </summary>
public interface IAutomationRuleStore
{
    /// <summary>Fügt eine neue Rule atomar ein.</summary>
    Task AddAsync(AutomationRule rule, CancellationToken cancellationToken = default);

    Task AddAsync(
        AutomationRule rule,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");

    /// <summary>Lädt eine Rule oder <see langword="null"/>.</summary>
    Task<AutomationRule?> GetAsync(AutomationRuleId ruleId, CancellationToken cancellationToken = default);

    /// <summary>Lädt alle Rules in deterministischer Management-Reihenfolge.</summary>
    Task<IReadOnlyList<AutomationRule>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sperrt eine Rule mit einer atomaren Management-Transaktion, mutiert sie und speichert
    /// geänderte Konfiguration samt Kindzeilen vollständig.
    /// </summary>
    Task<AutomationRule?> MutateAsync(
        AutomationRuleId ruleId,
        Func<AutomationRule, bool> mutation,
        CancellationToken cancellationToken = default);

    /// <summary>Mutiert eine Rule innerhalb einer vom Kompositor gehaltenen Transaktion.</summary>
    Task<AutomationRule?> MutateAsync(
        AutomationRuleId ruleId,
        Func<AutomationRule, bool> mutation,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");
}
