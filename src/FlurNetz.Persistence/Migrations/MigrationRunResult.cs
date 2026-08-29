namespace FlurNetz.Persistence.Migrations;

public sealed record MigrationRunResult(int AppliedCount, int SkippedCount);
