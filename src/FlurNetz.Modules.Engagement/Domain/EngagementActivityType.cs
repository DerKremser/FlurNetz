namespace FlurNetz.Modules.Engagement.Domain;

/// <summary>
/// Beschreibt die aktuell unterstützte normalisierte Engagement-Aktivität.
/// </summary>
/// <remarks>
/// Der explizite Wert hält die Domain-Repräsentation stabil. Die Persistenz verwendet dennoch
/// den fachlichen Code <c>message</c> und ist nicht vom Enum-Ordinal abhängig.
/// </remarks>
public enum EngagementActivityType
{
    /// <summary>
    /// Eine Community-Identität hat eine Nachricht erzeugt.
    /// </summary>
    Message = 1
}
