using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Automation.Domain;

/// <summary>
/// Eine einzelne, typisierte V1-Action mit stabiler Ausführungsposition.
/// </summary>
public sealed record AutomationAction
{
    /// <summary>Höchste zulässige Position.</summary>
    public const int MaximumPosition = 15;

    /// <summary>Maximale Titel-Länge in Unicode-Skalarwerten.</summary>
    public const int MaxNotificationTitleLength = 200;

    /// <summary>Maximale Nachrichten-Länge in Unicode-Skalarwerten.</summary>
    public const int MaxNotificationMessageLength = 2000;

    /// <summary>Erstellt und validiert eine Action.</summary>
    public AutomationAction(
        int position,
        string actionType,
        long? amount = null,
        string? title = null,
        string? message = null,
        OverlayChannelId? overlayChannelId = null,
        string? variant = null,
        int? durationMilliseconds = null)
    {
        if (position is < 0 or > MaximumPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Action-Positionen müssen zwischen 0 und 15 liegen.");
        }

        Position = position;
        ActionType = EnsureType(actionType);
        Amount = amount;
        Title = title is null ? null : AutomationText.Required(title, nameof(title), "Der Automation-Notification-Titel", MaxNotificationTitleLength);
        Message = AutomationText.Optional(message, nameof(message), "Die Automation-Notification-Nachricht", MaxNotificationMessageLength);
        OverlayChannelId = overlayChannelId;
        Variant = variant;
        DurationMilliseconds = durationMilliseconds;
        EnsureValueShape(ActionType, Amount, Title, Message, OverlayChannelId, Variant, DurationMilliseconds);
    }

    /// <summary>Position innerhalb der Rule.</summary>
    public int Position { get; }

    /// <summary>Stabiler Action-Typ.</summary>
    public string ActionType { get; }

    /// <summary>Alias für API-/Mapping-Code.</summary>
    public string Type => ActionType;

    /// <summary>Economy-Betrag.</summary>
    public long? Amount { get; }

    /// <summary>Notification-Titel.</summary>
    public string? Title { get; }

    /// <summary>Alias für die persistierte Notification-Titelspalte.</summary>
    public string? NotificationTitle => Title;

    /// <summary>Optionale Notification-Nachricht.</summary>
    public string? Message { get; }

    /// <summary>Overlay-Zielkanal für <c>overlay.alert</c>.</summary>
    public OverlayChannelId? OverlayChannelId { get; }

    /// <summary>Overlay-Variante für <c>overlay.alert</c>.</summary>
    public string? Variant { get; }

    /// <summary>Overlay-Anzeigezeit für <c>overlay.alert</c>.</summary>
    public int? DurationMilliseconds { get; }

    /// <summary>Alias für die persistierte Notification-Nachrichtenspalte.</summary>
    public string? NotificationMessage => Message;

    /// <summary>Erstellt eine validierte Action.</summary>
    public static AutomationAction Create(int position, string actionType, long? amount = null, string? title = null, string? message = null, OverlayChannelId? overlayChannelId = null, string? variant = null, int? durationMilliseconds = null) =>
        new(position, actionType, amount, title, message, overlayChannelId, variant, durationMilliseconds);

    /// <summary>Rehydriert eine persistierte Action ohne stilles Reparieren.</summary>
    public static AutomationAction Rehydrate(int position, string actionType, long? amount = null, string? title = null, string? message = null, OverlayChannelId? overlayChannelId = null, string? variant = null, int? durationMilliseconds = null) =>
        new(position, actionType, amount, title, message, overlayChannelId, variant, durationMilliseconds);

    private static string EnsureType(string? actionType)
    {
        if (actionType is not (AutomationActionTypes.EconomyCredit or AutomationActionTypes.NotificationCreate or AutomationActionTypes.OverlayAlert))
        {
            throw new ArgumentException("Der Action-Typ ist für Automation V1 nicht unterstützt.", nameof(actionType));
        }

        return actionType;
    }

    private static void EnsureValueShape(string type, long? amount, string? title, string? message, OverlayChannelId? overlayChannelId, string? variant, int? durationMilliseconds)
    {
        var valid = type switch
        {
            AutomationActionTypes.EconomyCredit => amount is > 0 && title is null && message is null && overlayChannelId is null && variant is null && durationMilliseconds is null,
            AutomationActionTypes.NotificationCreate => amount is null && title is not null && overlayChannelId is null && variant is null && durationMilliseconds is null,
            AutomationActionTypes.OverlayAlert => amount is null
                && title is not null
                && overlayChannelId.HasValue
                && overlayChannelId.Value.Value != Guid.Empty
                && OverlayAlertVariant.IsSupported(variant)
                && durationMilliseconds is >= OverlayAlertDurationRules.MinimumMilliseconds and <= OverlayAlertDurationRules.MaximumMilliseconds,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException("Die Wertfelder der Action passen nicht exakt zu ihrem Action-Typ.");
        }
    }
}
