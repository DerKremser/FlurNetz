using FlurNetz.Modules.Rewards.Domain;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Zeigt an, dass ein Reward-Package eine unbekannte Definition referenzieren würde.
/// </summary>
public sealed class RewardDefinitionNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Erstellt den Fehler für eine oder mehrere fehlende Definitionen.
    /// </summary>
    /// <param name="missingDefinitionIds">Die nicht gefundenen Definitionen.</param>
    /// <exception cref="ArgumentNullException">Wenn die Sammlung fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn die Sammlung leer ist.</exception>
    public RewardDefinitionNotFoundException(IEnumerable<RewardDefinitionId> missingDefinitionIds)
        : this(CopyDefinitionIds(missingDefinitionIds))
    {
    }

    private RewardDefinitionNotFoundException(RewardDefinitionId[] missingDefinitionIds)
        : base(CreateMessage(missingDefinitionIds))
    {
        if (missingDefinitionIds.Length == 0)
        {
            throw new ArgumentException(
                "Mindestens eine fehlende Reward-Definition wird benötigt.",
                "missingDefinitionIds");
        }

        MissingDefinitionIds = Array.AsReadOnly(missingDefinitionIds);
    }

    /// <summary>
    /// Liefert die fehlenden Definitionen in der Reihenfolge der Prüfung.
    /// </summary>
    public IReadOnlyList<RewardDefinitionId> MissingDefinitionIds { get; }

    private static string CreateMessage(IEnumerable<RewardDefinitionId> missingDefinitionIds)
    {
        ArgumentNullException.ThrowIfNull(missingDefinitionIds);

        return "Mindestens eine Reward-Definition wurde nicht gefunden: "
            + string.Join(", ", missingDefinitionIds.Select(id => id.Value));
    }

    private static RewardDefinitionId[] CopyDefinitionIds(
        IEnumerable<RewardDefinitionId> missingDefinitionIds)
    {
        ArgumentNullException.ThrowIfNull(missingDefinitionIds);
        return missingDefinitionIds.ToArray();
    }
}
