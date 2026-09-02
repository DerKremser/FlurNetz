using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Lädt die Execution-History einer Rule mit Keyset-Pagination.</summary>
public sealed class ListAutomationExecutions
{
    /// <summary>Standardseitengröße.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Minimale Seitengröße.</summary>
    public const int MinimumPageSize = 1;

    /// <summary>Maximale Seitengröße.</summary>
    public const int MaximumPageSize = 100;

    private readonly IAutomationExecutionHistoryStore store;

    /// <summary>Erstellt den Use Case.</summary>
    public ListAutomationExecutions(IAutomationExecutionHistoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Lädt eine newest-first-Seite.</summary>
    public async Task<AutomationExecutionPage> ExecuteAsync(
        AutomationRuleId ruleId,
        AutomationExecutionCursor? cursor = null,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var validId = AutomationRuleId.Create(ruleId.Value);
        if (pageSize is < MinimumPageSize or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, $"Die Seitengröße muss zwischen {MinimumPageSize} und {MaximumPageSize} liegen.");
        }

        if (cursor is not null && cursor.AutomationRuleId != validId)
        {
            throw new ArgumentException("Der Execution-Cursor gehört zu einer anderen Rule.", nameof(cursor));
        }

        var executions = await store.ListAsync(validId, cursor, pageSize + 1, cancellationToken).ConfigureAwait(false);
        var hasMore = executions.Count > pageSize;
        var items = hasMore ? executions.Take(pageSize).ToArray() : executions.ToArray();
        var nextCursor = hasMore ? new AutomationExecutionCursor(validId, items[^1].ExecutedAtUtc, items[^1].Id) : null;
        return new AutomationExecutionPage(items, nextCursor);
    }
}
