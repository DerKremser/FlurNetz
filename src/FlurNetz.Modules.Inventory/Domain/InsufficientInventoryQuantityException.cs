namespace FlurNetz.Modules.Inventory.Domain;

/// <summary>
/// Zeigt an, dass eine fachlich gültige Entnahme den vorhandenen Inventory-Bestand übersteigen würde.
/// </summary>
public sealed class InsufficientInventoryQuantityException : Exception
{
    /// <summary>
    /// Erstellt die fachliche Ausnahme für einen unzureichenden Inventory-Bestand.
    /// </summary>
    public InsufficientInventoryQuantityException()
        : base("Der Inventory-Bestand reicht für die Entnahme nicht aus.")
    {
    }
}
