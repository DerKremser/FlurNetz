using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Einmalige Ausgabe eines neu erzeugten Source Keys.</summary>
public sealed record OverlayChannelSecret(OverlayChannel Channel, string SourceKey);

/// <summary>Auflösung einer Browser Source inklusive sicherem Startcursor.</summary>
public sealed record OverlayBrowserSourceResolution(OverlayChannel Channel, OverlayAlertCursor StartCursor);
