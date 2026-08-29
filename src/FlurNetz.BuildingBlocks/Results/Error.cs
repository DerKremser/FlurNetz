using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.BuildingBlocks.Results;

/// <summary>
/// Beschreibt einen erwartbaren Fehler einer Operation mit stabilem Code und lesbarer Nachricht.
/// </summary>
/// <remarks>
/// Ein <see cref="Error"/> ist ein Datenwert für fachlich oder technisch erwartbare
/// Fehlersituationen. Er enthält keine Logik und entscheidet nicht darüber, wie ein Host
/// den Fehler anzeigt oder protokolliert.
/// </remarks>
public sealed record Error
{
    /// <summary>
    /// Erstellt einen Fehlerwert.
    /// </summary>
    /// <param name="code">Stabiler, maschinenlesbarer Fehlercode.</param>
    /// <param name="message">Nichtleere, menschenlesbare Fehlernachricht.</param>
    /// <exception cref="ArgumentException">Wenn Code oder Nachricht leer bzw. nur aus Leerzeichen bestehen.</exception>
    public Error(string code, string message)
    {
        Code = Guard.NotNullOrWhiteSpace(code, nameof(code));
        Message = Guard.NotNullOrWhiteSpace(message, nameof(message));
    }

    /// <summary>
    /// Gibt den stabilen maschinenlesbaren Fehlercode zurück.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gibt die menschenlesbare Fehlernachricht zurück.
    /// </summary>
    public string Message { get; }
}
