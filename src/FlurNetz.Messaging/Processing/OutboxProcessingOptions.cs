namespace FlurNetz.Messaging.Processing;

/// <summary>
/// Begrenzt Batch-Größe, Leasing und Wiederholungen des Outbox-Processors.
/// </summary>
public sealed record OutboxProcessingOptions
{
    /// <summary>
    /// Maximale Anzahl in einem Lauf zu claimender Nachrichten.
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Maximale Anzahl von Zustellversuchen einschließlich des ersten Versuchs.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Wartezeit bis zum nächsten Versuch einer fehlgeschlagenen Nachricht.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Dauer eines Claims, bevor eine abgestürzte Verarbeitung erneut aufgegriffen werden darf.
    /// </summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Prüft die technischen Grenzen der Optionen.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Wenn eine Option ungültig ist.</exception>
    public void Validate()
    {
        if (BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "The batch size must be positive.");
        }

        if (MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), MaxAttempts, "The maximum attempts must be positive.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), RetryDelay, "The retry delay cannot be negative.");
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration), LeaseDuration, "The lease duration must be positive.");
        }
    }
}
