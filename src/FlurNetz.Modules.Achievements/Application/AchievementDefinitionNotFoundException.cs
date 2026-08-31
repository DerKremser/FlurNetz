using FlurNetz.Modules.Achievements.Domain;

namespace FlurNetz.Modules.Achievements.Application;

/// <summary>
/// Zeigt an, dass eine angeforderte Achievement-Definition nicht im eigenen Katalog existiert.
/// </summary>
public sealed class AchievementDefinitionNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Erstellt einen Not-Found-Fehler für eine gültige Definition-ID.
    /// </summary>
    /// <param name="achievementDefinitionId">Die unbekannte Definition-ID.</param>
    /// <exception cref="ArgumentException">Wenn die ID leer ist.</exception>
    public AchievementDefinitionNotFoundException(AchievementDefinitionId achievementDefinitionId)
        : base($"Die Achievement-Definition '{achievementDefinitionId.Value}' wurde nicht gefunden.")
    {
        if (achievementDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Achievement-Definition-ID darf nicht leer sein.",
                nameof(achievementDefinitionId));
        }

        AchievementDefinitionId = achievementDefinitionId;
    }

    /// <summary>
    /// Liefert die nicht gefundene Definition-ID.
    /// </summary>
    public AchievementDefinitionId AchievementDefinitionId { get; }
}
