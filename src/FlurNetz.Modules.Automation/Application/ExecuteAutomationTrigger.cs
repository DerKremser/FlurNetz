using System.Data.Common;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Contracts;
using FlurNetz.Modules.Overlay.Contracts;
using Microsoft.Extensions.Logging;

namespace FlurNetz.Modules.Automation.Application;

/// <summary>
/// Führt alle passenden aktiven Rules eines Trigger-Snapshots atomar und deterministisch aus.
/// </summary>
public sealed class ExecuteAutomationTrigger
{
    private readonly IAutomationRuntimeStore runtimeStore;
    private readonly IEconomyBalanceCredit economyCredit;
    private readonly ICommunityNotificationCreate notificationCreate;
    private readonly IOverlayAlertPublish? overlayAlertPublish;
    private readonly IClock clock;
    private readonly ILogger<ExecuteAutomationTrigger>? logger;

    /// <summary>Erstellt den Runtime-Use-Case.</summary>
    public ExecuteAutomationTrigger(
        IAutomationRuntimeStore runtimeStore,
        IEconomyBalanceCredit economyCredit,
        ICommunityNotificationCreate notificationCreate,
        IClock clock,
        IOverlayAlertPublish? overlayAlertPublish = null,
        ILogger<ExecuteAutomationTrigger>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeStore);
        ArgumentNullException.ThrowIfNull(economyCredit);
        ArgumentNullException.ThrowIfNull(notificationCreate);
        ArgumentNullException.ThrowIfNull(clock);
        this.runtimeStore = runtimeStore;
        this.economyCredit = economyCredit;
        this.notificationCreate = notificationCreate;
        this.clock = clock;
        this.overlayAlertPublish = overlayAlertPublish;
        this.logger = logger;
    }

    /// <summary>
    /// Lädt den stabil gelockten Rule-Snapshot und führt dessen Actions in Positionsreihenfolge aus.
    /// Die Methode eröffnet, committet und rollbackt keine Transaktion.
    /// </summary>
    public async Task ExecuteAsync(
        AutomationTriggerSnapshot snapshot,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var rules = await runtimeStore.LoadActiveRulesAsync(
                snapshot.TriggerType,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var rule in rules.OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.AutomationRuleId.Value))
        {
            if (!rule.Matches(snapshot))
            {
                continue;
            }

            var execution = AutomationExecution.Create(
                AutomationExecutionId.New(),
                rule.AutomationRuleId,
                snapshot,
                Canonicalize(clock.UtcNow));
            var reserved = await runtimeStore.ReserveExecutionAsync(
                    execution,
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!reserved)
            {
                continue;
            }

            foreach (var action in rule.Actions.OrderBy(action => action.Position))
            {
                switch (action.ActionType)
                {
                    case AutomationActionTypes.EconomyCredit:
                        await economyCredit.CreditAsync(
                                CommunityIdentityId.Create(snapshot.CommunityIdentityId),
                                action.Amount!.Value,
                                connection,
                                transaction,
                                cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case AutomationActionTypes.NotificationCreate:
                        await notificationCreate.CreateAsync(
                                CommunityIdentityId.Create(snapshot.CommunityIdentityId),
                                "automation.rule-executed",
                                action.Title!,
                                action.Message,
                                "automation.execution",
                                execution.Id.Value.ToString("D"),
                                connection,
                                transaction,
                                cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case AutomationActionTypes.OverlayAlert:
                        if (overlayAlertPublish is null)
                        {
                            throw new InvalidOperationException("Die Overlay-Publish-Capability ist in der Automation-Komposition nicht registriert.");
                        }

                        // Suppression (disabled, archived oder missing) ist ein fachlich erwarteter
                        // Konfigurationszustand und wird bewusst nicht als technischer Fehler geworfen.
                        var publishResult = await overlayAlertPublish.PublishAsync(
                                new OverlayAlertPublishRequest(
                                    action.OverlayChannelId!.Value,
                                    action.Title!,
                                    action.Message,
                                    action.Variant!,
                                    action.DurationMilliseconds!.Value),
                                connection,
                                transaction,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!publishResult.IsPublished)
                        {
                            logger?.LogWarning(
                                "Overlay-Alert für Automation-Rule wurde unterdrückt. Channel: {OverlayChannelId}, Status: {Status}",
                                action.OverlayChannelId.Value.Value,
                                publishResult.Status);
                        }
                        break;

                    default:
                        throw new InvalidOperationException("Ein unbekannter Automation-Action-Typ wurde rehydriert.");
                }
            }
        }
    }

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}

/// <summary>Benannte Fassade für die interne Automation-Rule-Engine.</summary>
public sealed class AutomationRuleEngine
{
    private readonly ExecuteAutomationTrigger execute;

    /// <summary>Erstellt die Engine-Fassade.</summary>
    public AutomationRuleEngine(ExecuteAutomationTrigger execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = execute;
    }

    /// <summary>Delegiert die Ausführung an den gemeinsamen Trigger-Use-Case.</summary>
    public Task ExecuteAsync(AutomationTriggerSnapshot snapshot, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default) =>
        execute.ExecuteAsync(snapshot, connection, transaction, cancellationToken);
}
