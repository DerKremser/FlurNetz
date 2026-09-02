namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Validiert fachliche UTC-Zeitpunkte mit PostgreSQL-Mikrosekundenpräzision.</summary>
internal static class OverlayTimestamp
{
    public static DateTimeOffset Ensure(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Overlay-Zeitpunkte müssen in UTC und PostgreSQL-kompatibler Mikrosekundenpräzision vorliegen.",
                parameterName);
        }

        return value;
    }
}
