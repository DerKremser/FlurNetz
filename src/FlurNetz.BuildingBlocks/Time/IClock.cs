namespace FlurNetz.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
