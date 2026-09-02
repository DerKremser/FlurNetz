using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Processing;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Progression;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Notifications;
using FlurNetz.Modules.Automation;
using AutomationEngagementConsumer = FlurNetz.Modules.Automation.Application.EngagementMessageRecordedAutomationConsumer;
using AutomationShopConsumer = FlurNetz.Modules.Automation.Application.ShopPurchaseCompletedAutomationConsumer;
using FlurNetz.Modules.Economy;
using NotificationsShopPurchaseHandler = FlurNetz.Modules.Notifications.Application.ShopPurchaseCompletedIntegrationEventHandler;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlurNetz.Worker;

/// <summary>
/// Markiert den ausführbaren Messaging-Worker für den Generic Host und die Test-Komposition.
/// </summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>
    /// Baut den Worker-Host auf und startet seine dauerhafte Outbox-Verarbeitung.
    /// </summary>
    /// <param name="args">Argumente des Hostprozesses.</param>
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        try
        {
            await host.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            await DisposeHostAsync(host).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Erstellt dieselbe technische Host-Komposition, die auch die Integrationstests verwenden.
    /// </summary>
    /// <param name="args">Argumente für die Standard-.NET-Konfiguration.</param>
    /// <returns>Der noch nicht gebaute Generic Host Builder.</returns>
    internal static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) => ConfigureServices(context.Configuration, services));
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Der Worker ist die äußerste Kompositionsgrenze. Er verdrahtet ausschließlich die
        // technischen Grundlagen, die bekannten Event-Contracts und den vorhandenen Runtime-Consumer.
        services.AddSingleton<PostgreSqlOptions>(serviceProvider =>
        {
            var connectionString = configuration
                .GetConnectionString("FlurNetz")
                ?? throw new InvalidOperationException(
                    "The PostgreSQL connection string 'ConnectionStrings:FlurNetz' is not configured.");

            var options = new PostgreSqlOptions(connectionString);
            options.Validate();
            return options;
        });
        services.AddSingleton<PostgreSqlConnectionFactory>();
        services.AddSingleton<IPostgreSqlConnectionFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgreSqlConnectionFactory>());

        services.AddSingleton<IntegrationEventTypeRegistry>(_ =>
        {
            var registry = new IntegrationEventTypeRegistry();
            registry.Register<MessageEngagementRecordedIntegrationEvent>(
                MessageEngagementRecordedIntegrationEvent.MessageType,
                MessageEngagementRecordedIntegrationEvent.SchemaVersion);
            registry.Register<ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedIntegrationEvent.MessageType,
                ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
            return registry;
        });
        services.AddSingleton<IIntegrationEventTypeRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<IntegrationEventTypeRegistry>());
        services.AddSingleton<IntegrationEventJsonSerializer>(serviceProvider =>
            new IntegrationEventJsonSerializer(
                serviceProvider.GetRequiredService<IIntegrationEventTypeRegistry>()));
        services.AddSingleton<IIntegrationEventSerializer>(serviceProvider =>
            serviceProvider.GetRequiredService<IntegrationEventJsonSerializer>());

        services.AddSingleton<OutboxProcessingOptions>();
        services.AddScoped<OutboxProcessor>();
        services.AddSingleton<IMigrationSource, MessagingMigrationSource>();
        services.AddProgressionModule();
        services.AddNotificationsModule();
        services.AddNotificationsConsumer();
        services.AddEconomyCreditCapability();
        services.AddAutomationModule();
        services.AddAutomationConsumers();
        services.AddSingleton<MigrationRunner>(serviceProvider =>
            new MigrationRunner(
                serviceProvider.GetRequiredService<IPostgreSqlConnectionFactory>(),
                serviceProvider.GetServices<IMigrationSource>()));

        services.AddOptions<MessagingWorkerOptions>()
            .BindConfiguration(MessagingWorkerOptions.SectionName)
            .Validate(options => options.IsValid(),
                "MessagingWorker IdleDelay and FailureDelay must be greater than zero.")
            .ValidateOnStart();
        services.AddHostedService<WorkerStartupService>();
        services.AddHostedService<MessagingWorker>();
    }

    private static async ValueTask DisposeHostAsync(IHost host)
    {
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        host.Dispose();
    }

    /// <summary>
    /// Führt die vor dem Processing erforderliche Migration und Kompositionsprüfung aus.
    /// </summary>
    private sealed class WorkerStartupService(
        MigrationRunner migrationRunner,
        IServiceScopeFactory scopeFactory,
        IIntegrationEventTypeRegistry registry,
        ILogger<WorkerStartupService> logger) : IHostedService
    {
        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("FlurNetz Worker wird gestartet.");

            try
            {
                var migrationResult = await migrationRunner
                    .RunAsync(cancellationToken)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Worker-Startup-Migrationen erfolgreich ausgeführt. AppliedCount: {AppliedCount}, SkippedCount: {SkippedCount}.",
                    migrationResult.AppliedCount,
                    migrationResult.SkippedCount);

                ValidateComposition();
                logger.LogInformation(
                    "Worker-Komposition validiert: Registry für Engagement- und Shop-Event, Progression-, Notifications- und Automation-Consumer sowie OutboxProcessor sind bereit.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogCritical(
                    exception,
                    "FlurNetz Worker konnte Startup-Migrationen oder Kompositionsvalidierung nicht abschließen.");
                throw;
            }
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void ValidateComposition()
        {
            var descriptor = registry.Resolve(
                MessageEngagementRecordedIntegrationEvent.MessageType,
                MessageEngagementRecordedIntegrationEvent.SchemaVersion);
            if (descriptor.ClrType != typeof(MessageEngagementRecordedIntegrationEvent))
            {
                throw new InvalidOperationException(
                    "The registered engagement message does not resolve to its expected contract type.");
            }

            var shopDescriptor = registry.Resolve(
                ShopPurchaseCompletedIntegrationEvent.MessageType,
                ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
            if (shopDescriptor.ClrType != typeof(ShopPurchaseCompletedIntegrationEvent))
            {
                throw new InvalidOperationException(
                    "The registered shop purchase message does not resolve to its expected contract type.");
            }

            using var scope = scopeFactory.CreateScope();
            var registrations = scope.ServiceProvider
                .GetServices<IIntegrationEventHandlerRegistration>()
                .ToArray();
            if (!registrations.Any(registration =>
                registration.EventType == typeof(MessageEngagementRecordedIntegrationEvent)
                && registration.ConsumerName == MessageEngagementRecordedIntegrationEventHandler.ConsumerName))
            {
                throw new InvalidOperationException(
                    $"The Progression consumer registration for {MessageEngagementRecordedIntegrationEvent.MessageType} is missing.");
            }

            if (!registrations.Any(registration =>
                registration.EventType == typeof(MessageEngagementRecordedIntegrationEvent)
                && registration.ConsumerName == AutomationEngagementConsumer.ConsumerName))
            {
                throw new InvalidOperationException(
                    $"The Automation consumer registration for {MessageEngagementRecordedIntegrationEvent.MessageType} is missing.");
            }

            if (!registrations.Any(registration =>
                registration.EventType == typeof(ShopPurchaseCompletedIntegrationEvent)
                && registration.ConsumerName == NotificationsShopPurchaseHandler.ConsumerName))
            {
                throw new InvalidOperationException(
                    $"The Notifications consumer registration for {ShopPurchaseCompletedIntegrationEvent.MessageType} is missing.");
            }

            if (!registrations.Any(registration =>
                registration.EventType == typeof(ShopPurchaseCompletedIntegrationEvent)
                && registration.ConsumerName == AutomationShopConsumer.ConsumerName))
            {
                throw new InvalidOperationException(
                    $"The Automation consumer registration for {ShopPurchaseCompletedIntegrationEvent.MessageType} is missing.");
            }

            _ = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            _ = scope.ServiceProvider.GetRequiredService<FlurNetz.Modules.Economy.Contracts.IEconomyBalanceCredit>();
            _ = scope.ServiceProvider.GetRequiredService<FlurNetz.Modules.Notifications.Contracts.ICommunityNotificationCreate>();
            _ = scope.ServiceProvider.GetRequiredService<FlurNetz.Modules.Automation.Application.ExecuteAutomationTrigger>();
        }
    }
}
