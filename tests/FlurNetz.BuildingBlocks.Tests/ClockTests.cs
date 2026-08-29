using FlurNetz.BuildingBlocks.Time;

namespace FlurNetz.BuildingBlocks.Tests;

public sealed class ClockTests
{
    [Fact]
    public void Clock_ExposesUtcNow()
    {
        var expected = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        // Eine feste Uhr macht den Vertrag der Zeitabstraktion reproduzierbar statt von der Systemzeit abhängig.
        IClock clock = new FixedClock(expected);

        Assert.Equal(expected, clock.UtcNow);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
