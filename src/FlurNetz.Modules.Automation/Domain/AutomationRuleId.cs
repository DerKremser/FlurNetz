namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Bezeichnet eine persistierte Automation-Regel.
/// </summary>
public readonly record struct AutomationRuleId
{
    private readonly Guid value;

    private AutomationRuleId(Guid value) => this.value = value;

    /// <summary>Gibt den zugrunde liegenden GUID-Wert zurück.</summary>
    public Guid Value => value;

    /// <summary>Erstellt eine Regel-ID aus einer nicht leeren GUID.</summary>
    public static AutomationRuleId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Die Automation-Rule-ID darf nicht leer sein.", nameof(value));
        }

        return new AutomationRuleId(value);
    }

    /// <summary>Erzeugt eine neue serverseitige Regel-ID.</summary>
    public static AutomationRuleId New() => Create(Guid.NewGuid());
}
