namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Beschreibt die minimale fachliche Bedeutung einer konfigurierten Reward-Wirkung.
/// </summary>
/// <remarks>
/// Rewards beschreibt, welche Wirkung später gewährt werden soll, besitzt aber nicht den
/// resultierenden Zustand des Zielmoduls. Deshalb enthält die gemeinsame Basis nur ihre
/// eigene Kennung. XP bleiben vollständig im Progression-Modul und sind kein Reward-Typ.
/// </remarks>
public abstract class RewardDefinition
{
    /// <summary>
    /// Initialisiert eine gültige Reward-Definition.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung der Definition.</param>
    /// <exception cref="ArgumentException">Wenn <paramref name="id"/> leer ist.</exception>
    protected RewardDefinition(RewardDefinitionId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Reward-Definition benötigt eine nicht leere ID.",
                nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// Liefert die unveränderliche Kennung dieser Reward-Definition.
    /// </summary>
    public RewardDefinitionId Id { get; }
}
