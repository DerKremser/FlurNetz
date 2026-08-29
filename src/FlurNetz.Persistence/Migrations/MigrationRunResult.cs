namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Fasst das Ergebnis eines Migrationslaufs zusammen.
/// </summary>
/// <param name="AppliedCount">Anzahl neu erfolgreich angewendeter Migrationen.</param>
/// <param name="SkippedCount">Anzahl unveränderter, bereits registrierter Migrationen.</param>
public sealed record MigrationRunResult(int AppliedCount, int SkippedCount);
