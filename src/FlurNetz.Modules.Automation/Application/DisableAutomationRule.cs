using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Deaktiviert eine Automation-Rule.</summary>
public sealed class DisableAutomationRule
{
    private readonly IAutomationRuleStore store;
    private readonly IClock clock;

    /// <summary>Erstellt den Use Case.</summary>
    public DisableAutomationRule(IAutomationRuleStore store, IClock clock)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Deaktiviert die Rule oder führt auf Disabled/Archived einen No-op aus.</summary>
    public async Task ExecuteAsync(AutomationRuleId ruleId, CancellationToken cancellationToken = default)
    {
        var validId = AutomationRuleId.Create(ruleId.Value);
        var result = await store.MutateAsync(validId, rule => rule.Disable(Canonicalize(clock.UtcNow)), cancellationToken).ConfigureAwait(false);
        if (result is null) throw new AutomationRuleNotFoundException(validId);
    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
