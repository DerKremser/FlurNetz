namespace FlurNetz.BuildingBlocks.Time;

/// <summary>
/// Abstrahiert die aktuelle UTC-Zeit für deterministische und testbare Komponenten.
/// </summary>
/// <remarks>
/// Aufrufer hängen nicht direkt von der Systemuhr ab. Tests können dadurch einen festen
/// Zeitpunkt verwenden, während die Produktionsimplementierung die reale UTC-Zeit liefert.
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Gibt den aktuellen Zeitpunkt in UTC zurück.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
