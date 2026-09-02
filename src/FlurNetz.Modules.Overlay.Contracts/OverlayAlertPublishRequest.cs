using System.Data.Common;

namespace FlurNetz.Modules.Overlay.Contracts;

/// <summary>Caller-neutraler Auftrag zum Persistieren eines Overlay-Alerts.</summary>
public sealed record OverlayAlertPublishRequest(
    OverlayChannelId ChannelId,
    string Title,
    string? Message,
    string Variant,
    int DurationMilliseconds,
    string? SourceType = null,
    string? SourceId = null);

/// <summary>Ergebnis eines Overlay-Publish-Aufrufs.</summary>
public sealed record OverlayAlertPublishResult(OverlayAlertPublishStatus Status, Guid? AlertId = null)
{
    /// <summary>Gibt an, ob tatsächlich ein Alert persistiert wurde.</summary>
    public bool IsPublished => Status == OverlayAlertPublishStatus.Published;
}

/// <summary>Fachliche Ergebnisse des transaction-aware Overlay-Publishers.</summary>
public enum OverlayAlertPublishStatus
{
    /// <summary>Der Alert wurde persistiert.</summary>
    Published = 0,

    /// <summary>Der Zielkanal existiert nicht.</summary>
    ChannelNotFound = 1,

    /// <summary>Der Zielkanal ist deaktiviert.</summary>
    ChannelDisabled = 2,

    /// <summary>Der Zielkanal ist archiviert.</summary>
    ChannelArchived = 3
}

/// <summary>
/// Schmale Capability, die innerhalb einer bereits bestehenden Datenbanktransaktion arbeitet.
/// </summary>
public interface IOverlayAlertPublish
{
    /// <summary>
    /// Persistiert einen Alert ohne eigene Verbindung, Transaktion, Commit oder Rollback.
    /// </summary>
    Task<OverlayAlertPublishResult> PublishAsync(
        OverlayAlertPublishRequest request,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
