namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Fasst eine verpflichtende Menge von Reward-Definitionen fachlich zusammen.
/// </summary>
/// <remarks>
/// Ein Package ist nur gültig, wenn es mindestens eine Definition enthält. Die Komponenten
/// sind später gemeinsam verpflichtend: Entweder gelingen alle oder keine. Die technische
/// Collection bleibt in ihrer Eingabereihenfolge stabil, aber die Package-Semantik verspricht
/// keine fachliche Ausführungsreihenfolge.
///
/// Doppelte Definitionen werden ausgeschlossen, weil die spätere Grant-Eindeutigkeit pro
/// <c>SourceType</c>, <c>SourceId</c> und <c>RewardDefinitionId</c> gilt. Eine Definition
/// darf in einem Package daher nicht mehrfach dieselbe Ausführung verlangen.
/// </remarks>
public sealed class RewardPackage
{
    private RewardPackage(
        RewardPackageId id,
        IReadOnlyList<RewardDefinitionId> rewardDefinitionIds)
    {
        Id = id;
        RewardDefinitionIds = rewardDefinitionIds;
    }

    /// <summary>
    /// Liefert die unveränderliche Kennung dieses Reward-Packages.
    /// </summary>
    public RewardPackageId Id { get; }

    /// <summary>
    /// Liefert die nicht leere, doppelfreie Menge der enthaltenen Definitionen.
    /// </summary>
    public IReadOnlyList<RewardDefinitionId> RewardDefinitionIds { get; }

    /// <summary>
    /// Erstellt ein gültiges Reward-Package.
    /// </summary>
    /// <param name="id">Die nicht leere Kennung des Packages.</param>
    /// <param name="rewardDefinitionIds">Die mindestens eine gültige Definition enthaltende Sammlung.</param>
    /// <returns>Ein unveränderliches Reward-Package.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="rewardDefinitionIds"/> fehlt.</exception>
    /// <exception cref="ArgumentException">
    /// Wenn die Package-ID leer ist, keine Definition enthalten ist, eine Definition-ID leer ist
    /// oder eine Definition mehrfach enthalten ist.
    /// </exception>
    public static RewardPackage Create(
        RewardPackageId id,
        IEnumerable<RewardDefinitionId> rewardDefinitionIds)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Reward-Package benötigt eine nicht leere ID.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(rewardDefinitionIds);

        var copiedDefinitionIds = rewardDefinitionIds.ToArray();

        if (copiedDefinitionIds.Length == 0)
        {
            throw new ArgumentException(
                "Ein Reward-Package muss mindestens eine Reward-Definition enthalten.",
                nameof(rewardDefinitionIds));
        }

        if (copiedDefinitionIds.Any(definitionId => definitionId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "Ein Reward-Package darf keine leere Reward-Definitions-ID enthalten.",
                nameof(rewardDefinitionIds));
        }

        if (copiedDefinitionIds.Distinct().Count() != copiedDefinitionIds.Length)
        {
            throw new ArgumentException(
                "Ein Reward-Package darf dieselbe Reward-Definition nicht mehrfach enthalten.",
                nameof(rewardDefinitionIds));
        }

        return new RewardPackage(
            id,
            Array.AsReadOnly(copiedDefinitionIds));
    }
}
