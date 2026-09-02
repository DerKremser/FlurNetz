using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>Versionierbarer Seek-Cursor für die Execution-History einer Rule.</summary>
public sealed record AutomationExecutionCursor
{
    /// <summary>Erstellt einen an die Rule gebundenen Cursor.</summary>
    public AutomationExecutionCursor(AutomationRuleId automationRuleId, DateTimeOffset executedAtUtc, AutomationExecutionId id)
    {
        if (automationRuleId.Value == Guid.Empty || id.Value == Guid.Empty)
        {
            throw new ArgumentException("Rule- und Execution-ID des Cursors dürfen nicht leer sein.");
        }

        if (executedAtUtc.Offset != TimeSpan.Zero || executedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException("Der Execution-Cursor muss einen UTC-Mikrosekundenzeitpunkt enthalten.", nameof(executedAtUtc));
        }

        AutomationRuleId = automationRuleId;
        ExecutedAtUtc = executedAtUtc;
        Id = id;
    }

    /// <summary>Rule-Bindung.</summary>
    public AutomationRuleId AutomationRuleId { get; }

    /// <summary>Letzter Execution-Zeitpunkt der Seite.</summary>
    public DateTimeOffset ExecutedAtUtc { get; }

    /// <summary>Letzte Execution-ID der Seite.</summary>
    public AutomationExecutionId Id { get; }
}
