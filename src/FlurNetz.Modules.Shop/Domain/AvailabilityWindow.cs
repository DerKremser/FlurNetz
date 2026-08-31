namespace FlurNetz.Modules.Shop.Domain;

/// <summary>
/// Beschreibt ein optional begrenztes Zeitfenster für die Verfügbarkeit eines Shop-Angebots.
/// </summary>
/// <remarks>
/// Das Intervall ist halboffen: Der Start ist inklusive, das Ende exklusiv. Die Struktur liest
/// keine Systemzeit; der Prüfzeitpunkt wird immer vom Aufrufer übergeben.
/// </remarks>
public readonly record struct AvailabilityWindow
{
    private readonly DateTimeOffset? _availableFrom;
    private readonly DateTimeOffset? _availableUntil;

    /// <summary>
    /// Erstellt ein gültiges Verfügbarkeitsfenster.
    /// </summary>
    /// <param name="availableFrom">Optionaler inklusiver Beginn.</param>
    /// <param name="availableUntil">Optionales exklusives Ende.</param>
    /// <exception cref="ArgumentException">
    /// Wenn beide Zeitpunkte gesetzt sind und der Beginn nicht vor dem Ende liegt.
    /// </exception>
    public AvailabilityWindow(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil)
    {
        EnsureValidRange(availableFrom, availableUntil);
        _availableFrom = availableFrom;
        _availableUntil = availableUntil;
    }

    /// <summary>
    /// Liefert den optionalen inklusiven Beginn.
    /// </summary>
    public DateTimeOffset? AvailableFrom => _availableFrom;

    /// <summary>
    /// Liefert das optionale exklusive Ende.
    /// </summary>
    public DateTimeOffset? AvailableUntil => _availableUntil;

    /// <summary>
    /// Erstellt ein gültiges Verfügbarkeitsfenster.
    /// </summary>
    public static AvailabilityWindow Create(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil) => new(availableFrom, availableUntil);

    /// <summary>
    /// Prüft die Verfügbarkeit zum übergebenen Zeitpunkt nach der Semantik
    /// <c>[AvailableFrom, AvailableUntil)</c>.
    /// </summary>
    /// <param name="at">Der zu prüfende Zeitpunkt.</param>
    /// <returns>
    /// <see langword="true"/>, wenn der Zeitpunkt innerhalb des Fensters liegt.
    /// </returns>
    public bool IsAvailableAt(DateTimeOffset at)
    {
        return (!_availableFrom.HasValue || at >= _availableFrom.Value)
            && (!_availableUntil.HasValue || at < _availableUntil.Value);
    }

    /// <summary>
    /// Prüft, ob der übergebene Zeitpunkt innerhalb des Fensters liegt.
    /// </summary>
    public bool Contains(DateTimeOffset at) => IsAvailableAt(at);

    private static void EnsureValidRange(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil)
    {
        if (availableFrom.HasValue && availableUntil.HasValue && availableFrom >= availableUntil)
        {
            throw new ArgumentException(
                "Der Beginn des Verfügbarkeitsfensters muss vor seinem Ende liegen.",
                nameof(availableUntil));
        }
    }
}
