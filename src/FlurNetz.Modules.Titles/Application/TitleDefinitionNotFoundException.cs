using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Zeigt an, dass eine angeforderte Title-Definition nicht im Katalog existiert.
/// </summary>
public sealed class TitleDefinitionNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Erstellt einen Not-Found-Fehler für eine gültige Title-Definition-ID.
    /// </summary>
    public TitleDefinitionNotFoundException(TitleDefinitionId titleDefinitionId)
        : base($"Die Title-Definition '{titleDefinitionId.Value}' wurde nicht gefunden.")
    {
        if (titleDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Title-Definition-ID darf nicht leer sein.",
                nameof(titleDefinitionId));
        }

        TitleDefinitionId = titleDefinitionId;
    }

    /// <summary>
    /// Liefert die nicht gefundene fachliche Kennung.
    /// </summary>
    public TitleDefinitionId TitleDefinitionId { get; }
}
