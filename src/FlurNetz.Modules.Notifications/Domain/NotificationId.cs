namespace FlurNetz.Modules.Notifications.Domain;

/// <summary>
/// Stabile, implementation-eigene Identität einer persönlichen Notification.
/// </summary>
public readonly record struct NotificationId
{
    private readonly Guid value;

    private NotificationId(Guid value)
    {
        this.value = value;
    }

    /// <summary>
    /// Liefert den zugrunde liegenden GUID-Wert.
    /// </summary>
    public Guid Value => value;

    /// <summary>
    /// Erstellt eine Notification-ID aus einer nicht leeren GUID.
    /// </summary>
    public static NotificationId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Notification-ID darf nicht leer sein.",
                nameof(value));
        }

        return new NotificationId(value);
    }

    /// <summary>
    /// Erzeugt eine neue Notification-ID.
    /// </summary>
    public static NotificationId New() => Create(Guid.NewGuid());
}
