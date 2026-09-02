namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Bezeichnet einen erfolgreich reservierten Automation-Lauf.
/// </summary>
public readonly record struct AutomationExecutionId
{
    private readonly Guid value;

    private AutomationExecutionId(Guid value) => this.value = value;

    /// <summary>Gibt den zugrunde liegenden GUID-Wert zurück.</summary>
    public Guid Value => value;

    /// <summary>Erstellt eine Execution-ID aus einer nicht leeren GUID.</summary>
    public static AutomationExecutionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Automation-Execution-ID darf nicht leer sein.", nameof(value));
        }

        return new AutomationExecutionId(value);
    }

    /// <summary>Erzeugt eine neue Execution-ID.</summary>
    public static AutomationExecutionId New() => Create(Guid.NewGuid());
}
