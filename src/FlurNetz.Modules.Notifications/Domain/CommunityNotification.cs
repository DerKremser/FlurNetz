using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Notifications.Domain;

/// <summary>
/// Repräsentiert eine persistierte persönliche In-App-Notification.
/// </summary>
/// <remarks>
/// Alle anzeigbaren Werte und die optionale Herkunft sind historische Snapshots. Das Modell
/// liest daher beim späteren Laden keine Daten aus dem Ursprungsmodul nach.
/// </remarks>
public sealed class CommunityNotification
{
    public const int MaxNotificationTypeLength = 100;
    public const int MaxTitleLength = 200;
    public const int MaxMessageLength = 2000;

    private CommunityNotification(
        NotificationId id,
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? readAtUtc)
    {
        Id = id;
        CommunityIdentityId = communityIdentityId;
        NotificationType = notificationType;
        Title = title;
        Message = message;
        SourceReference = sourceReference;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = readAtUtc;
    }

    public NotificationId Id { get; }

    public CommunityIdentityId CommunityIdentityId { get; }

    public string NotificationType { get; }

    public string Title { get; }

    public string? Message { get; }

    public NotificationSourceReference? SourceReference { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    /// <summary>
    /// Erstellt eine neue Notification mit vollständig validiertem Snapshot.
    /// </summary>
    public static CommunityNotification Create(
        NotificationId id,
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference,
        DateTimeOffset createdAtUtc) =>
        CreateCore(
            id,
            communityIdentityId,
            notificationType,
            title,
            message,
            sourceReference,
            createdAtUtc,
            null,
            normalizeText: true);

    /// <summary>
    /// Rekonstruiert den vollständigen Zustand einer persistierten Notification.
    /// </summary>
    public static CommunityNotification Rehydrate(
        NotificationId id,
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? readAtUtc) =>
        CreateCore(
            id,
            communityIdentityId,
            notificationType,
            title,
            message,
            sourceReference,
            createdAtUtc,
            readAtUtc,
            normalizeText: false);

    /// <summary>
    /// Markiert die Notification mit einem kanonischen Zeitpunkt als gelesen.
    /// </summary>
    public bool MarkRead(DateTimeOffset readAtUtc)
    {
        var canonical = EnsureValidUtc(readAtUtc, nameof(readAtUtc));
        if (ReadAtUtc.HasValue)
        {
            return false;
        }

        ReadAtUtc = canonical;
        return true;
    }

    /// <summary>
    /// Markiert die Notification als ungelesen.
    /// </summary>
    public bool MarkUnread()
    {
        if (!ReadAtUtc.HasValue)
        {
            return false;
        }

        ReadAtUtc = null;
        return true;
    }

    private static CommunityNotification CreateCore(
        NotificationId id,
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        NotificationSourceReference? sourceReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? readAtUtc,
        bool normalizeText)
    {
        var validId = NotificationId.Create(id.Value);
        var validIdentityId = CommunityIdentityId.Create(communityIdentityId.Value);

        var validNotificationType = normalizeText
            ? NotificationText.Required(
                notificationType,
                nameof(notificationType),
                "Der NotificationType",
                MaxNotificationTypeLength)
            : EnsureCanonicalRequired(
                notificationType,
                nameof(notificationType),
                "der NotificationType",
                MaxNotificationTypeLength);
        var validTitle = normalizeText
            ? NotificationText.Required(title, nameof(title), "Der Notification-Titel", MaxTitleLength)
            : EnsureCanonicalRequired(title, nameof(title), "der Notification-Titel", MaxTitleLength);
        var validMessage = normalizeText
            ? NotificationText.Optional(message, nameof(message), "Die Notification-Nachricht", MaxMessageLength)
            : EnsureCanonicalOptional(message, nameof(message), "die Notification-Nachricht", MaxMessageLength);

        var validCreatedAtUtc = EnsureValidUtc(createdAtUtc, nameof(createdAtUtc));
        DateTimeOffset? validReadAtUtc = readAtUtc.HasValue
            ? EnsureValidUtc(readAtUtc.Value, nameof(readAtUtc))
            : null;

        return new CommunityNotification(
            validId,
            validIdentityId,
            validNotificationType,
            validTitle,
            validMessage,
            sourceReference,
            validCreatedAtUtc,
            validReadAtUtc);
    }

    private static string EnsureCanonicalRequired(
        string value,
        string parameterName,
        string fieldName,
        int maximumScalarCount)
    {
        NotificationText.EnsureCanonical(value, parameterName, fieldName, maximumScalarCount, allowNull: false);
        return value;
    }

    private static string? EnsureCanonicalOptional(
        string? value,
        string parameterName,
        string fieldName,
        int maximumScalarCount)
    {
        NotificationText.EnsureCanonical(value, parameterName, fieldName, maximumScalarCount, allowNull: true);
        return value;
    }

    private static DateTimeOffset EnsureValidUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Fachliche Notification-Zeitpunkte müssen in UTC vorliegen.",
                parameterName);
        }

        if (value.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "Fachliche Notification-Zeitpunkte müssen PostgreSQL-kompatible Mikrosekundenpräzision besitzen.",
                parameterName);
        }

        return value;
    }
}
