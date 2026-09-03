using System.Data.Common;
using FlurNetz.Modules.Rewards.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Rewards.Application;

/// <summary>
/// Definiert die kleinen Persistenzoperationen für Reward-Konfiguration.
/// </summary>
/// <remarks>
/// Der Port enthält nur die im aktuellen Konfigurations-Use-Case benötigten Operationen.
/// Ein generisches Repository oder eine vorsorgliche CRUD-Abstraktion würde die noch nicht
/// vorhandene Verwaltungs-API vorwegnehmen.
/// </remarks>
public interface IRewardCatalogStore
{
    /// <summary>
    /// Persistiert eine Economy-Balance-Reward-Definition.
    /// </summary>
    /// <param name="definition">Die fachlich bereits validierte Definition.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Eine Aufgabe, die nach dem Commit abgeschlossen ist.</returns>
    Task AddDefinitionAsync(
        EconomyBalanceRewardDefinition definition,
        CancellationToken cancellationToken = default);

    Task AddDefinitionAsync(
        EconomyBalanceRewardDefinition definition,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");

    /// <summary>
    /// Ermittelt die Definitionen, die noch nicht im Rewards-Katalog existieren.
    /// </summary>
    /// <param name="rewardDefinitionIds">Die zu prüfenden Definitionen.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Lesevorgangs.</param>
    /// <returns>Die unbekannten Definitionen in stabiler Eingabereihenfolge.</returns>
    Task<IReadOnlyList<RewardDefinitionId>> FindMissingDefinitionIdsAsync(
        IEnumerable<RewardDefinitionId> rewardDefinitionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persistiert ein Package und seine Membership atomar.
    /// </summary>
    /// <param name="package">Das fachlich validierte Package.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankvorgangs.</param>
    /// <returns>Eine Aufgabe, die nach dem gemeinsamen Commit abgeschlossen ist.</returns>
    Task AddPackageAsync(
        RewardPackage package,
        CancellationToken cancellationToken = default);

    Task AddPackageAsync(
        RewardPackage package,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");

    /// <summary>Lädt alle aktuell unterstützten Reward-Definitionen.</summary>
    Task<IReadOnlyList<RewardDefinition>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen Definitions-Read.");

    /// <summary>Lädt Packages einschließlich ihrer Membership.</summary>
    Task<IReadOnlyList<RewardPackage>> ListPackagesAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen Package-Read.");

    /// <summary>Lädt Grant-History optional auf eine Community-Identity begrenzt.</summary>
    Task<IReadOnlyList<RewardGrant>> ListGrantsAsync(
        CommunityIdentityId? communityIdentityId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen Grant-History-Read.");
}
