namespace FlurNetz.Worker;

/// <summary>
/// Steuert ausschließlich Polling und technisches Backoff des Worker-Hosts.
/// </summary>
/// <remarks>
/// Batch-Größe, Lease und Message-Level-Retry bleiben bewusst in
/// <see cref="FlurNetz.Messaging.Processing.OutboxProcessingOptions"/>.
/// </remarks>
public sealed record MessagingWorkerOptions
{
    /// <summary>
    /// Name des Konfigurationsabschnitts für die Worker-Laufzeitoptionen.
    /// </summary>
    public const string SectionName = "MessagingWorker";

    /// <summary>
    /// Wartezeit bei einer leeren Outbox.
    /// </summary>
    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Wartezeit nach einem unerwarteten technischen Fehler eines Batch-Laufs.
    /// </summary>
    public TimeSpan FailureDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Prüft, dass der Worker nicht in eine Busy-Polling-Schleife geraten kann.
    /// </summary>
    /// <returns><see langword="true"/>, wenn beide Verzögerungen positiv sind.</returns>
    public bool IsValid() => IdleDelay > TimeSpan.Zero && FailureDelay > TimeSpan.Zero;

    /// <summary>
    /// Prüft die konfigurierten Laufzeitoptionen und meldet den ersten ungültigen Wert.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Wenn eine Verzögerung nicht positiv ist.</exception>
    public void Validate()
    {
        if (IdleDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IdleDelay),
                IdleDelay,
                "The idle delay must be greater than zero.");
        }

        if (FailureDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FailureDelay),
                FailureDelay,
                "The failure delay must be greater than zero.");
        }
    }
}
