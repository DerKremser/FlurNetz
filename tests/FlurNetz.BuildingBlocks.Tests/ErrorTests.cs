using FlurNetz.BuildingBlocks.Results;

namespace FlurNetz.BuildingBlocks.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void Error_ExposesCodeAndMessage()
    {
        var error = new Error("test.code", "Testnachricht.");

        Assert.Equal("test.code", error.Code);
        Assert.Equal("Testnachricht.", error.Message);
    }

    [Fact]
    public void Error_RejectsEmptyCode()
    {
        Assert.Throws<ArgumentException>(() => new Error(" ", "Nachricht"));
    }

    [Fact]
    public void Error_RejectsEmptyMessage()
    {
        Assert.Throws<ArgumentException>(() => new Error("code", " "));
    }
}
