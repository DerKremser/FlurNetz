using FlurNetz.Api.Endpoints;
using FlurNetz.Modules.Identity;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.Configuration;

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

        // Das Modul registriert seine eigenen Use Cases, Adapter und Migrationsquelle.
        builder.Services.AddIdentityModule();

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

        app.MapIdentityEndpoints();
        await app.RunAsync();
    }
}
