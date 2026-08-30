namespace FlurNetz.Modules.Rewards.Domain;

/// <summary>
/// Beschreibt die fachliche Herkunft eines Reward-Grants.
/// </summary>
/// <remarks>
/// <c>SourceType</c> bleibt ein stabiler String statt eines Rewards-eigenen Enums. Dadurch
/// können auslösende Domänen wie Achievements, Progression, Daily oder Administration neue
/// Herkunftstypen liefern, ohne das Rewards-Domainmodell zu ändern. Event-, Message- oder
/// Inbox-IDs gehören nicht in diese fachliche Herkunft.
/// </remarks>
public sealed record RewardSource
{
    private RewardSource(string sourceType, string sourceId)
    {
        SourceType = sourceType;
        SourceId = sourceId;
    }

    /// <summary>
    /// Liefert den stabilen fachlichen Typ der Quelle.
    /// </summary>
    public string SourceType { get; }

    /// <summary>
    /// Liefert die von der Quelle vergebene fachliche Kennung.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Erstellt eine gültige fachliche Reward-Quelle.
    /// </summary>
    /// <param name="sourceType">Der nicht leere und nicht aus Leerzeichen bestehende Quellentyp.</param>
    /// <param name="sourceId">Die nicht leere und nicht aus Leerzeichen bestehende Quellenkennung.</param>
    /// <returns>Eine unveränderliche Reward-Quelle.</returns>
    /// <exception cref="ArgumentException">Wenn ein Parameter fehlt, leer oder nur aus Leerzeichen besteht.</exception>
    public static RewardSource Create(string? sourceType, string? sourceId)
    {
        return new RewardSource(
            EnsureText(sourceType, nameof(sourceType)),
            EnsureText(sourceId, nameof(sourceId)));
    }

    private static string EnsureText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Der Wert darf nicht leer oder aus Leerzeichen bestehen.",
                parameterName);
        }

        return value;
    }
}
