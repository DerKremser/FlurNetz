using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.BuildingBlocks.Tests;

public sealed class GuardTests
{
    [Fact]
    public void NotNull_ReturnsProvidedReference()
    {
        var value = new Sample();

        Assert.Same(value, Guard.NotNull(value, nameof(value)));
    }

    [Fact]
    public void NotNull_RejectsNullReference()
    {
        Assert.Throws<ArgumentNullException>(() => Guard.NotNull<Sample>(null, "value"));
    }

    [Fact]
    public void NotNullOrWhiteSpace_ReturnsProvidedText()
    {
        const string value = "valid";

        Assert.Equal(value, Guard.NotNullOrWhiteSpace(value, nameof(value)));
    }

    [Fact]
    public void NotNullOrWhiteSpace_RejectsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(" ", "value"));
    }

    private sealed class Sample
    {
    }
}
