using FlurNetz.BuildingBlocks.Guards;

namespace FlurNetz.BuildingBlocks.Results;

public sealed record Error
{
    public Error(string code, string message)
    {
        Code = Guard.NotNullOrWhiteSpace(code, nameof(code));
        Message = Guard.NotNullOrWhiteSpace(message, nameof(message));
    }

    public string Code { get; }

    public string Message { get; }
}
