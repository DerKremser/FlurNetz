using FlurNetz.Api.Administration;
using FlurNetz.Api.Endpoints;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Migrations;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Administration;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Achievements;
using FlurNetz.Modules.Economy;
using FlurNetz.Modules.Identity;
using FlurNetz.Modules.Integrations;
using FlurNetz.Modules.Inventory;
using FlurNetz.Modules.Notifications;
using FlurNetz.Modules.Automation;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop;
using FlurNetz.Modules.Overlay;
using FlurNetz.Modules.Progression;
using FlurNetz.Modules.Rewards;
using FlurNetz.Modules.Titles;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FlurNetz.Api;

/// <summary>
/// Markiert den ausführbaren FlurNetz-API-Host für den Testhost und die Composition-Root-Grenze.
/// </summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>
    /// Baut den API-Host auf, führt die Startmigrationen aus und startet den HTTP-Listener.
    /// </summary>
    /// <param name="args">Argumente des Hostprozesses.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddProblemDetails();
        builder.Services.AddRazorPages();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-FlurNetz.AntiForgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
        });
        builder.Services.AddSingleton<AdminSetupGateConfiguration>(serviceProvider =>
            new AdminSetupGateConfiguration(
                serviceProvider.GetRequiredService<IConfiguration>()["Administration:Setup:Secret"]));
        builder.Services.AddAdminAuthentication(builder.Environment);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("AdminLogin", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy("AdminSetup", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"setup:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        // Die technische Verbindungsfabrik gehört in den Composition Root, damit alle Module
        // dieselbe Persistence-Grundlage verwenden und keine eigene Datenbankinfrastruktur aufbauen.
        builder.Services.AddSingleton<PostgreSqlOptions>(serviceProvider =>
        {
            var connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("FlurNetz")
                ?? throw new InvalidOperationException(
                    "The PostgreSQL connection string 'ConnectionStrings:FlurNetz' is not configured.");

            return new PostgreSqlOptions(connectionString);
        });
        builder.Services.AddSingleton<PostgreSqlConnectionFactory>();
        builder.Services.AddSingleton<IPostgreSqlConnectionFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgreSqlConnectionFactory>());
        builder.Services.AddSingleton<MigrationRunner>(serviceProvider =>
            new MigrationRunner(
                serviceProvider.GetRequiredService<IPostgreSqlConnectionFactory>(),
                serviceProvider.GetServices<IMigrationSource>()));

        // Die Module registrieren ihre Use Cases, Adapter und bestehenden Migrationsquellen.
        builder.Services.AddAdministrationModule();
        builder.Services.AddIdentityModule();
        builder.Services.AddIntegrationsModule();
        builder.Services.AddEconomyModule();
        builder.Services.AddInventoryModule();
        builder.Services.AddProgressionReadModule();
        builder.Services.AddAchievementsModule();
        builder.Services.AddRewardsModule();
        builder.Services.AddTitlesModule();
        builder.Services.AddShopModule();
        builder.Services.AddNotificationsModule();
        builder.Services.AddAutomationModule();
        builder.Services.AddOverlayModule();

        // Der API-Host ist in diesem Slice ausschließlich Producer. Es werden nur der vom
        // Shop-Purchase erzeugte Contract und die dafür benötigten Outbox-Komponenten verdrahtet.
        builder.Services.AddSingleton<IntegrationEventTypeRegistry>(_ =>
        {
            var registry = new IntegrationEventTypeRegistry();
            registry.Register<ShopPurchaseCompletedIntegrationEvent>(
                ShopPurchaseCompletedIntegrationEvent.MessageType,
                ShopPurchaseCompletedIntegrationEvent.SchemaVersion);
            return registry;
        });
        builder.Services.AddSingleton<IIntegrationEventTypeRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<IntegrationEventTypeRegistry>());
        builder.Services.AddSingleton<IntegrationEventJsonSerializer>(serviceProvider =>
            new IntegrationEventJsonSerializer(
                serviceProvider.GetRequiredService<IIntegrationEventTypeRegistry>()));
        builder.Services.AddSingleton<IIntegrationEventSerializer>(serviceProvider =>
            serviceProvider.GetRequiredService<IntegrationEventJsonSerializer>());
        builder.Services.AddSingleton<IIntegrationEventPublisher, PostgreSqlOutboxPublisher>();
        builder.Services.AddSingleton<IMigrationSource, MessagingMigrationSource>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }

        try
        {
            // Migrationen laufen vor dem ersten Listener-Start. Ein Fehler beendet den Host,
            // damit kein Prozess als betriebsbereit erscheint, dessen Datenbank nicht bereit ist.
            await using var scope = app.Services.CreateAsyncScope();
            var migrationRunner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
            await migrationRunner.RunAsync(app.Lifetime.ApplicationStopping);

        }
        catch (Exception exception)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(exception, "FlurNetz API konnte die erforderlichen Migrationen nicht ausführen.");
            throw;
        }

        app.UseRouting();
        app.UseMiddleware<AdminSecurityHeadersMiddleware>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AdminExecutionContextMiddleware>();
        app.UseMiddleware<AdminAntiforgeryMiddleware>();
        app.UseAntiforgery();

        app.MapIdentityEndpoints();
        app.MapAdminIdentityEndpoints();
        app.MapAdminEconomyEndpoints();
        app.MapAdminProgressionInventoryEndpoints();
        app.MapAdminAchievementTitleEndpoints();
        app.MapAdminRewardsEndpoints();
        app.MapIntegrationsManagementEndpoints();
        app.MapShopEndpoints();
        app.MapShopManagementEndpoints();
        app.MapNotificationEndpoints();
        app.MapAutomationManagementEndpoints();
        app.MapOverlayEndpoints();
        app.MapAdminAccountEndpoints();
        app.MapRazorPages();
        await app.RunAsync();
    }
}
