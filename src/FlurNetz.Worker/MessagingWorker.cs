using FlurNetz.Messaging.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlurNetz.Worker;

/// <summary>
/// Führt die technische Outbox-Verarbeitung kontinuierlich über den bestehenden Processor aus.
/// </summary>
/// <remarks>
/// Der BackgroundService hält bewusst nur die Scope-Fabrik. Scoped Consumer, Store und
/// Processor werden pro Batch neu aufgelöst, damit ihre Inbox-Transaktionen nicht über die
/// Lebensdauer des Singleton-Workers hinaus gehalten werden.
/// </remarks>
public sealed class MessagingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingWorkerOptions> options,
    ILogger<MessagingWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerOptions = options.Value;
        workerOptions.Validate();

        logger.LogInformation("Messaging-Processing-Loop gestartet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                OutboxProcessingResult result;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                    result = await processor
                        .ProcessBatchAsync(stoppingToken)
                        .ConfigureAwait(false);
                }

                if (result.ClaimedCount > 0)
                {
                    logger.LogInformation(
                        "Outbox-Batch verarbeitet. ClaimedCount: {ClaimedCount}, ProcessedCount: {ProcessedCount}, RetriedCount: {RetriedCount}, FailedCount: {FailedCount}, DuplicateDeliveryCount: {DuplicateDeliveryCount}.",
                        result.ClaimedCount,
                        result.ProcessedCount,
                        result.RetriedCount,
                        result.FailedCount,
                        result.DuplicateDeliveryCount);
                }
                else
                {
                    await Task.Delay(workerOptions.IdleDelay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unerwarteter Fehler im Outbox-Batch. Nächster Versuch nach {FailureDelay}.",
                    workerOptions.FailureDelay);

                try
                {
                    await Task.Delay(workerOptions.FailureDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Messaging-Processing-Loop beendet.");
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("FlurNetz Worker wird beendet.");
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
