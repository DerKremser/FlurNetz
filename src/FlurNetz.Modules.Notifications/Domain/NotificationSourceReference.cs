namespace FlurNetz.Modules.Notifications.Domain;

/// <summary>
/// Optionale, notifications-eigene Referenz auf die fachliche Herkunft einer Notification.
/// </summary>
public sealed record NotificationSourceReference
{
    /// <summary>
    /// Maximale Länge des Herkunftstyps in Unicode-Skalarwerten.
    /// </summary>
    public const int MaxSourceTypeLength = 100;

    /// <summary>
    /// Maximale Länge der Herkunfts-ID in Unicode-Skalarwerten.
    /// </summary>
    public const int MaxSourceIdLength = 200;

    /// <summary>
    /// Erstellt eine validierte Herkunftsreferenz.
    /// </summary>
    public NotificationSourceReference(string sourceType, string sourceId)
    {
        SourceType = NotificationText.Required(
            sourceType,
            nameof(sourceType),
            "Der Notification-SourceType",
            MaxSourceTypeLength);
        SourceId = NotificationText.Required(
            sourceId,
            nameof(sourceId),
            "Die Notification-SourceId",
            MaxSourceIdLength);
    }

    /// <summary>
    /// Liefert den kanonischen logischen Herkunftstyp.
    /// </summary>
    public string SourceType { get; }

    /// <summary>
    /// Liefert die kanonische Herkunfts-ID.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Erstellt eine optionale Referenz. Nur zwei gesetzte Werte ergeben eine Referenz.
    /// </summary>
    public static NotificationSourceReference? Create(string? sourceType, string? sourceId)
    {
        if (sourceType is null && sourceId is null)
        {
            return null;
        }

        if (sourceType is null || sourceId is null)
        {
            throw new ArgumentException(
                "SourceType und SourceId müssen gemeinsam vorhanden sein oder gemeinsam fehlen.");
        }

        return new NotificationSourceReference(sourceType, sourceId);
    }

    /// <summary>
    /// Rekonstruiert eine bereits kanonische Persistenzrepräsentation.
    /// </summary>
    public static NotificationSourceReference Rehydrate(string sourceType, string sourceId)
    {
        NotificationText.EnsureCanonical(
            sourceType,
            nameof(sourceType),
            "der Notification-SourceType",
            MaxSourceTypeLength,
            allowNull: false);
        NotificationText.EnsureCanonical(
            sourceId,
            nameof(sourceId),
            "die Notification-SourceId",
            MaxSourceIdLength,
            allowNull: false);
        return new NotificationSourceReference(sourceType, sourceId);
    }
}
