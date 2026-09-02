using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Erstellt eine neue, zunächst deaktivierte Automation-Rule.</summary>
public sealed class CreateAutomationRule
{
    private readonly IAutomationRuleStore store;
    private readonly IClock clock;

    /// <summary>Erstellt den Use Case.</summary>
    public CreateAutomationRule(IAutomationRuleStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        this.store = store;
        this.clock = clock;
    }

    /// <summary>Validiert und persistiert eine neue Rule.</summary>
    public async Task<AutomationRule> ExecuteAsync(
        string displayName,
        string? description,
        string triggerType,
        IEnumerable<AutomationCondition> conditions,
        IEnumerable<AutomationAction> actions,
        int sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var now = Canonicalize(clock.UtcNow);
        var rule = AutomationRule.Create(
            AutomationRuleId.New(), displayName, description, triggerType, conditions, actions, sortOrder, now);
        await store.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        return rule;
    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
