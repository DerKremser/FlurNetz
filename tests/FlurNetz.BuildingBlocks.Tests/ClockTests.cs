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

    [Fact]
    public void SystemClock_ReturnsUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;

        var current = new SystemClock().UtcNow;

        var after = DateTimeOffset.UtcNow;
        Assert.Equal(TimeSpan.Zero, current.Offset);
        Assert.InRange(current, before, after);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
