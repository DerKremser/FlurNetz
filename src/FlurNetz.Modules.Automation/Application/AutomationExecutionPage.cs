using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Eine Seite der Automation-Execution-History.</summary>
public sealed class AutomationExecutionPage
{
    /// <summary>Erstellt eine unveränderliche History-Seite.</summary>
    public AutomationExecutionPage(IReadOnlyList<AutomationExecution> items, AutomationExecutionCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        NextCursor = nextCursor;
    }

    /// <summary>History-Items.</summary>
    public IReadOnlyList<AutomationExecution> Items { get; }

    /// <summary>Nächster Cursor oder null am Ende.</summary>
    public AutomationExecutionCursor? NextCursor { get; }
}
