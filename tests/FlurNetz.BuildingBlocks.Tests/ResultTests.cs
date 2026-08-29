using FlurNetz.BuildingBlocks.Results;

namespace FlurNetz.BuildingBlocks.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_RepresentsSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_RepresentsFailureAndCarriesError()
    {
        var error = new Error("test.failure", "Die Operation ist fehlgeschlagen.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_CarriesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFailure_CarriesErrorWithoutValue()
    {
        // Ein Failure darf keinen veralteten Erfolgswert mitführen; genau diese Zustandsinvariante wird hier festgehalten.
        var error = new Error("test.failure", "Die Operation ist fehlgeschlagen.");
        var result = Result<string>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Same(error, result.Error);
    }
}
