using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.BuildingBlocks.Results;

/// <summary>
/// Repräsentiert den Erfolg oder einen erwartbaren Fehler einer Operation ohne Rückgabewert.
/// </summary>
/// <remarks>
/// <see cref="Result"/> eignet sich für bekannte Operationsergebnisse, die der Aufrufer
/// behandeln soll. Exceptions bleiben für unerwartete Programm- oder Infrastrukturfehler
/// zuständig und werden durch diesen Typ nicht verschluckt.
/// </remarks>
public sealed class Result
{
    private Result(Error? error)
    {
        Error = error;
    }

    /// <summary>
    /// Gibt an, ob die Operation erfolgreich war.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Gibt an, ob ein erwartbarer Fehler vorliegt.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gibt den Fehler eines fehlgeschlagenen Ergebnisses zurück; bei Erfolg ist der Wert <see langword="null"/>.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Erzeugt ein erfolgreiches Ergebnis ohne Rückgabewert.
    /// </summary>
    /// <returns>Ein Ergebnis mit <see cref="IsSuccess"/> gleich <see langword="true"/>.</returns>
    public static Result Success() => new(null);

    /// <summary>
    /// Erzeugt ein fehlgeschlagenes Ergebnis mit einem erwartbaren Fehler.
    /// </summary>
    /// <param name="error">Der zu transportierende Fehler.</param>
    /// <returns>Ein Ergebnis mit <see cref="IsFailure"/> gleich <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="error"/> fehlt.</exception>
    public static Result Failure(Error error) => new(Guard.NotNull(error, nameof(error)));
}

/// <summary>
/// Repräsentiert den Erfolg mit einem Wert oder einen erwartbaren Fehler einer Operation.
/// </summary>
/// <typeparam name="T">Typ des möglichen Erfolgswerts.</typeparam>
/// <remarks>
/// Der Typ macht die beiden erlaubten Ergebniszustände explizit: Erfolg liefert einen Wert,
/// Fehler liefert einen <see cref="Error"/>. Unerwartete Programm- oder Infrastrukturfehler
/// bleiben Exceptions und werden nicht in ein erwartbares Ergebnis umgewandelt.
/// </remarks>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Gibt an, ob die Operation erfolgreich war.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Gibt an, ob ein erwartbarer Fehler vorliegt.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gibt den Erfolgswert zurück; bei einem Fehler wird der Standardwert des Typs geliefert.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gibt den Fehler eines fehlgeschlagenen Ergebnisses zurück; bei Erfolg ist der Wert <see langword="null"/>.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Erzeugt ein erfolgreiches Ergebnis mit einem Wert.
    /// </summary>
    /// <param name="value">Der Erfolgswert.</param>
    /// <returns>Ein Ergebnis mit <see cref="IsSuccess"/> gleich <see langword="true"/>.</returns>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>
    /// Erzeugt ein fehlgeschlagenes Ergebnis ohne Erfolgswert.
    /// </summary>
    /// <param name="error">Der zu transportierende Fehler.</param>
    /// <returns>Ein Ergebnis mit <see cref="IsFailure"/> gleich <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="error"/> fehlt.</exception>
    public static Result<T> Failure(Error error) => new(default, Guard.NotNull(error, nameof(error)));
}
