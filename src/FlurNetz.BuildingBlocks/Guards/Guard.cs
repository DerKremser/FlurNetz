namespace FlurNetz.BuildingBlocks.Guards;

/// <summary>
/// Bietet wiederverwendbare Eingabeprüfungen für technische Invarianten.
/// </summary>
/// <remarks>
/// Guards validieren lokale Voraussetzungen früh und einheitlich. Sie enthalten keine
/// fachliche Entscheidung und ersetzen keine Validierung komplexer Geschäftsregeln.
/// </remarks>
public static class Guard
{
    /// <summary>
    /// Gibt eine Referenz zurück oder bricht bei <see langword="null"/> mit einer Argumentexception ab.
    /// </summary>
    /// <typeparam name="T">Typ der erforderlichen Referenz.</typeparam>
    /// <param name="value">Zu prüfende Referenz.</param>
    /// <param name="parameterName">Name des geprüften Parameters.</param>
    /// <returns>Die nicht-<see langword="null"/>-Referenz aus <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="value"/> <see langword="null"/> ist.</exception>
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    /// <summary>
    /// Gibt einen Text zurück oder bricht bei leerem bzw. aus Leerzeichen bestehendem Text ab.
    /// </summary>
    /// <param name="value">Zu prüfender Text.</param>
    /// <param name="parameterName">Name des geprüften Parameters.</param>
    /// <returns>Der nichtleere Text aus <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer oder nur aus Leerzeichen besteht.</exception>
    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Der Wert darf nicht leer oder aus Leerzeichen bestehen.", parameterName)
            : value;
    }
}
