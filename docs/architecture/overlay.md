# Overlay V1

## Ownership

`FlurNetz.Modules.Overlay` besitzt die persistierte OBS-/Browser-Source-Alert-Pipeline:
Channels, Source-Key-Zuordnung, Alert-Snapshots, Replay-Cursor und den PostgreSQL-Zugriff.
Das Modul kennt keine Automation-, Shop-, Economy-, Notifications- oder Identity-
Implementierungen und liest keine fremden Tabellen. `Overlay.Contracts` veröffentlicht nur
die stabile Channel-ID, die V1-Varianten, die Duration-Grenzen und
`IOverlayAlertPublish` für fachlich neutrale Aufrufer.

Automation entscheidet ausschließlich, wann ein Alert ausgelöst wird. Overlay entscheidet,
auf welchem Channel und in welcher Darstellung er persistiert und ausgeliefert wird.

## Domainmodell und Lifecycle

`OverlayChannel` ist das Aggregate Root mit `OverlayChannelId`, `DisplayName`, optionaler
`Description`, `IsEnabled`, `IsArchived`, `CreatedAtUtc` und `UpdatedAtUtc`. Neue Channels
starten deaktiviert und nicht archiviert. Name und Beschreibung werden nach Unicode-
Skalarwerten kanonisch getrimmt und validiert; der Name ist auf 100, die Beschreibung auf
500 Skalarwerte begrenzt.

Enable, Disable, Archive sowie die Metadatenänderung sind gezielte Mutationen. Enable,
Disable und Archive sind idempotent; ein No-op ändert `UpdatedAtUtc` nicht. Archive ist
terminal, deaktiviert den Channel automatisch und verhindert erneute Aktivierung oder
Konfiguration. `Rehydrate` akzeptiert nur kanonische, invariantengerechte Zustände und
repariert keine beschädigten Persistenzwerte.

`OverlayAlert` ist ein unveränderlicher Snapshot mit implementation-owned
`OverlayAlertId`, Channel-ID, Titel, optionaler Nachricht, Variante, Dauer,
`SourceReference?`, `CreatedAtUtc` und `ExpiresAtUtc`. Titel und Nachricht sind auf 200
beziehungsweise 2.000 Unicode-Skalarwerte begrenzt. SourceType und SourceId sind auf 100
beziehungsweise 200 begrenzt und müssen gemeinsam vorhanden sein oder gemeinsam fehlen.
Alle Textwerte verbieten U+0000 und malformed UTF-16. Die erlaubten Varianten sind exakt
`default`, `success`, `warning` und `celebration`. Die Anzeigezeit liegt zwischen 1.000 und
30.000 ms; der Default beträgt 5.000 ms. Fachliche Zeitpunkte sind UTC mit
PostgreSQL-kompatibler Mikrosekundenpräzision. `ExpiresAtUtc` wird aus der Dauer berechnet
und bei Rehydrate exakt geprüft.

## Source Credentials

Ein Source Key ist technischer Credential-State und keine Domain-Eigenschaft. Er wird mit
32 kryptographisch sicheren Zufallsbytes erzeugt und URL-sicher ohne Padding kodiert.
PostgreSQL enthält ausschließlich den normalisierten SHA-256-Hash als lowercase Hexstring;
der Klartext wird weder geloggt noch im Domainaggregat geführt. Create und Rotate liefern
den Klartext genau einmal. GET und LIST liefern weder Key noch Hash. Rotation ersetzt den
Hash atomar und macht den alten Key sofort ungültig.

Ein deaktivierter Channel behält den Key und kann Preview-Alerts erhalten. Beim Archivieren
wird der Hash invalidiert; dadurch funktionieren Browser Source und Stream nicht mehr.

## Persistenz

Die Migration `Overlay:1:CreateOverlayChannelsAndAlerts` erzeugt ausschließlich die Tabellen
`overlay_channels` und `overlay_alerts`. Der einzige Foreign Key ist der modulinterne FK von
`overlay_alerts.overlay_channel_id` auf `overlay_channels.id`; Cross-Module-FKs gibt es nicht.
PostgreSQL-Constraints sichern IDs, Lifecycle, kanonische Texte, Varianten, Duration,
Source-Paar, Source-Text und die exakte Ablaufzeit zusätzlich zur Domain ab. Ein Index je
Channel unterstützt die verbindliche Reihenfolge `created_at_utc ASC, id ASC`.

Management-Mutationen laden den Channel mit `SELECT ... FOR UPDATE`; der technische
Source-Key-Hash wird dabei nur im Persistence-Row geführt. Alerts werden mit gezieltem
Dapper-/Npgsql-SQL geschrieben und gelesen, nicht über ein Generic Repository.

Das V1-Replay-Fenster ist zentral in `OverlayTransportDefaults.ReplayWindow` als zwei
Minuten definiert. Stream-Abfragen liefern nur Alerts innerhalb dieses Fensters, die noch
nicht abgelaufen sind. `ReadAfter` bereinigt zusätzlich höchstens 50 abgelaufene Alerts pro
Abfrage. Es gibt dafür keinen Scheduler und keine externe Queue. Die reine Anzeigezeit
bleibt auf maximal 30 Sekunden begrenzt; ein Reconnect kann innerhalb des längeren
Replay-Fensters noch gültige Alerts nachholen.

## Transaction-aware Publish

`IOverlayAlertPublish` nimmt ausschließlich caller-neutrale Contract-Werte sowie
`DbConnection` und `DbTransaction` entgegen. Die Overlay-Capability öffnet keine eigene
Connection und commitet oder rollbackt niemals selbst. Sie sperrt den Ziel-Channel,
unterdrückt einen fehlenden, deaktivierten oder archivierten Channel mit einem expliziten
`OverlayAlertPublishStatus` und schreibt einen gültigen Alert in dieselbe Transaktion.
Echte Persistenzfehler werden weitergegeben und rollen dadurch die gemeinsame Messaging-
Transaktion zurück.

Management- und Preview-Aufrufe besitzen eigene Transaktionen. Preview ist für nicht
archivierte Channels auch im deaktivierten Zustand erlaubt; normale Automation-Publishes
werden dort unterdrückt.

## Management-API

`FlurNetz.Api` mappt die interne Management-Grenze:

```text
GET  /api/admin/overlay/channels
GET  /api/admin/overlay/channels/{channelId}
POST /api/admin/overlay/channels
PUT  /api/admin/overlay/channels/{channelId}
POST /api/admin/overlay/channels/{channelId}/enable
POST /api/admin/overlay/channels/{channelId}/disable
POST /api/admin/overlay/channels/{channelId}/archive
POST /api/admin/overlay/channels/{channelId}/rotate-source-key
POST /api/admin/overlay/channels/{channelId}/alerts
```

Create liefert `201 Created`, Channel-Daten, den neuen Key genau einmal und eine relative
`/overlay/{sourceKey}`-URL. Rotate liefert den neuen Key und die neue URL genau einmal.
Get/List enthalten keine geheimen Werte. Ungültige Requests, unbekannte Channels und
fachliche Konflikte werden gemäß der bestehenden API-Konvention als ProblemDetails mit
400, 404 beziehungsweise 409 beantwortet. Die DTOs liegen ausschließlich im API-Projekt.

## OBS Browser Source

`GET /overlay/{sourceKey}` liefert eine direkt in OBS verwendbare, transparente HTML-Seite.
Sie ist responsiv für typische 1920x1080-Szenen, enthält nur kleines inline CSS und
JavaScript und bietet Titel, optionale Nachricht, die vier Varianten sowie Ein-/Ausblend-
Animationen. Alerts werden über eine FIFO-Queue nacheinander angezeigt. Text gelangt nur
über `textContent` in den DOM; Alert-Daten werden niemals als HTML interpretiert.

Die Seite nutzt `Cache-Control: no-store`, eine restriktive Content-Security-Policy ohne
externe Quellen und `Referrer-Policy: no-referrer`. Sie enthält keine Admin-Funktionalität,
keine Medien und kein Frontend-Framework. Beim Laden wird zuerst ein Tail-Cursor ermittelt,
damit Alerts zwischen HTML-Auflösung und Stream-Verbindung nicht doppelt erscheinen.

## SSE und Cursor/Reconnect

`GET /api/overlay/sources/{sourceKey}/stream` ist ein SSE-Stream ohne SignalR, Broker oder
zweiten Worker. Der Server pollt PostgreSQL alle 500 ms, sendet alle 15 Sekunden einen
Kommentar-Heartbeat und beendet den Stream bei Disconnect sauber. Die Antwort setzt
`text/event-stream`, `no-cache`, Keep-Alive und `X-Accel-Buffering: no`.

Jedes `overlay-alert`-Event besitzt eine opake, Base64URL-kodierte SSE-ID. Der Cursor bindet
Channel, `created_at_utc` und Alert-ID und setzt die SQL-Reihenfolge verlustfrei über
`created_at_utc ASC, id ASC` fort. `Last-Event-ID` hat Vorrang vor dem optionalen `after`-
Queryparameter. Ein frischer Stream startet hinter dem aktuellen Tail und spielt keine
alten Alerts ab; ein kurzer Reconnect liest noch gültige Alerts nach dem letzten Cursor
innerhalb des Replay-Fensters nach.

Mehrere Browser Sources desselben Channels werden unabhängig bedient. Die Browser-Seite
führt zusätzlich eine begrenzte ID-Deduplizierung von 256 Einträgen und stellt so auch bei
Transport-Wiederholungen keinen Alert doppelt dar. Ungültige oder archivierte Keys öffnen
weder Seite noch Stream.

## Automation-Integration

Automation unterstützt exakt den zusätzlichen Action-Typ `overlay.alert`. Die Action besitzt
explizite Werte für `OverlayChannelId`, `Title`, `Message?`, `Variant` und
`DurationMilliseconds` und verwendet dieselben Contract-Grenzen wie Overlay. Automation
referenziert ausschließlich `FlurNetz.Modules.Overlay.Contracts`, niemals die Overlay-
Implementierung.

Die veröffentlichte Migration `Automation:1:CreateAutomationRulesAndExecutions` bleibt
unverändert. `Automation:2:AddOverlayAlertAction` ergänzt eigene Overlay-Spalten und ersetzt
die Value-Shape-Constraints so, dass `overlay.alert` nur seine eigenen Felder und Economy-
und Notification-Actions keine Overlay-Felder besitzen. `ExecuteAutomationTrigger` schreibt
den Overlay-Alert über `IOverlayAlertPublish` in derselben Messaging-Transaktion wie Inbox,
Execution, Economy und Notifications. Suppression ist ein fachliches Ergebnis und kein
Poison-/Retry-Fehler; echte Persistenzfehler rollen die gesamte Transaktion zurück.
Action-Positionen bleiben lückenlos und deterministisch.

## Worker/API-Trennung

Der API-Host registriert Overlay und mappt Management, Browser Source und SSE. Er führt keine
Automation-Regeln aus und startet keinen OutboxProcessor. Der Worker registriert Overlay nur
für Runtime-Publish und die Auflösung der erweiterten Automation-Komposition. Er besitzt keine
Overlay-HTTP-, SSE- oder Admin-Verantwortung und führt keine Overlay-Consumer ein.

API und Worker registrieren jeweils die Overlay-Migration `Overlay:1` in ihrer eigenen
Composition Root; der MigrationRunner bleibt die gemeinsame technische Infrastruktur.

## Security-Grenzen

Die Management-Route ist eine ausdrücklich interne Grenze. Administration V1 schützt sie mit
dem getrennten Admin-Cookie-Scheme, expliziten Permissions, Anti-Forgery sowie Audit und
Operations. Vor externem Produktivbetrieb müssen zusätzlich Deployment-, Secret-Handling- und
Transport-/Proxy-Konfiguration durch eine separate Security Foundation geregelt werden.
Source Keys sind Bearer-Credentials und dürfen nicht in Logs, Tickets oder öffentliche
Storefronts gelangen.

## Tests

`FlurNetz.Modules.Overlay.Tests` prüft IDs, Unicode-Skalargrenzen, Emoji, U+0000,
malformed UTF-16, Rehydrate, Zeitkanonisierung, Duration, SourceReference, Varianten und
den vollständigen Channel-Lifecycle. Das echte Projekt
`FlurNetz.Modules.Overlay.IntegrationTests` prüft Migration/Checksum, Constraints,
Roundtrip, Hashing, Rotation, Archiv-Invalidierung, Row-Locking, Commit/Rollback,
Sortierung, Cursor, Expiry und Source-Identity gegen PostgreSQL.

API-Integrationstests decken Management, Secret-Ausgabe, Rotation, Browser-Header, Preview,
Archivierung und den SSE-Tail-/Live-Pfad ab. Automation-, Worker- und Workflow-Tests prüfen
Action-Shape, Migration 2, atomare Ausführung und die bestehende Hosttrennung.

## V1-Out-of-Scope

Nicht Bestandteil sind Audio, Bilder, Videos, benutzerdefiniertes HTML/CSS, Overlay-Editor,
Chat-Overlay, permanente XP-/Coin-/Goal-Widgets, plattformspezifische Twitch-/YouTube- /
Kick-/Discord-Logik, komplexe Templates, Variableninterpolation, SignalR, externer Broker,
Cloud-Push, freie Layout-Positionierung sowie eine allgemeine Community-Authentication/-
Authorization-Foundation.
