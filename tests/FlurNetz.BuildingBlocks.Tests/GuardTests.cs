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
        // Der Nullwert ist hier absichtlich, weil der Guard die technische Eingabeinvariante durchsetzen soll.
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
        // Auch nur aus Leerzeichen bestehende Werte sind für Bezeichner und Nachrichten ungültig.
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(" ", "value"));
    }

    private sealed class Sample
    {
    }
}
