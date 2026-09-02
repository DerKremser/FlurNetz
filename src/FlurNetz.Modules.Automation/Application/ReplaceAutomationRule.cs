using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Ersetzt die vollständige Konfiguration einer deaktivierten Rule.</summary>
public sealed class ReplaceAutomationRule
{
    private readonly IAutomationRuleStore store;
    private readonly IClock clock;

    /// <summary>Erstellt den Use Case.</summary>
    public ReplaceAutomationRule(IAutomationRuleStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    /// <summary>Führt ein atomisches Replace aus.</summary>
    public async Task ExecuteAsync(
        AutomationRuleId ruleId,
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var validId = AutomationRuleId.Create(ruleId.Value);
        var result = await store.MutateAsync(
                validId,
                rule =>
                {
                    if (rule.IsEnabled || rule.IsArchived)
                    {
                        throw new AutomationRuleConflictException(
                            validId,
                            "Eine aktive oder archivierte Automation-Rule kann nicht ersetzt werden.");
                    }

                    return rule.ReplaceConfiguration(
                        displayName, description, triggerType, conditions, actions, sortOrder, Canonicalize(clock.UtcNow));
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            throw new AutomationRuleNotFoundException(validId);
        }

    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
