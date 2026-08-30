namespace FlurNetz.BuildingBlocks.Time;

/// <summary>
/// Liefert die Systemzeit als UTC-Zeitquelle für produktive Komponenten.
/// </summary>
/// <remarks>
/// Die konkrete Uhr bleibt als domain-neutrale technische Implementierung in BuildingBlocks.
/// Fachliche Komponenten hängen weiterhin nur vom testbaren <see cref="IClock"/>-Vertrag ab.
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
