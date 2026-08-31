using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Migrations;
using FlurNetz.Modules.Achievements.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Modules.Achievements;

/// <summary>
/// Registriert die tatsächlich vorhandenen Komponenten des Achievements-Slices.
/// </summary>
/// <remarks>
/// Achievements registriert keine eigene Uhr und überschreibt damit keine globale Clock-
/// Konfiguration. API-, Worker-, Messaging- und weitere Modulverdrahtung ist nicht enthalten.
/// </remarks>
public static class AchievementsModule
{
    /// <summary>
    /// Registriert Stores, Use Cases und die Achievements-Migration.
    /// </summary>
    /// <param name="services">Der Dependency-Injection-Container des Composition Roots.</param>
    /// <returns>Die übergebene Service-Sammlung.</returns>
    public static IServiceCollection AddAchievementsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAchievementDefinitionStore, AchievementDefinitionStore>();
        services.AddScoped<ICommunityAchievementStore, CommunityAchievementStore>();
        services.AddScoped<CreateAchievementDefinition>();
        services.AddScoped<GetAchievementDefinition>();
        services.AddScoped<ListAchievementDefinitions>();
        services.AddScoped<RenameAchievementDefinition>();
        services.AddScoped<ChangeAchievementDescription>();
        services.AddScoped<UnlockCommunityAchievement>();
        services.AddScoped<GetCommunityAchievement>();
        services.AddScoped<ListCommunityAchievements>();
        services.AddSingleton<IMigrationSource, AchievementsMigrationSource>();

        return services;
    }
}
