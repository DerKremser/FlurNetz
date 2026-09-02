# Notifications – persönliche In-App-Inbox V1

## Verantwortung und Ownership

`FlurNetz.Modules.Notifications` ist ein Consumer-/Policy-Modul. Es besitzt ausschließlich
persönliche In-App-Benachrichtigungen, deren Snapshot und deren Read-/Unread-Lebenszyklus.
Ein Ursprungsvorgang bleibt im Ursprungsmodul: Der Shop erzeugt weiterhin seinen eigenen Kauf
und das unveränderte `shop.purchase-completed`-Event. Der Shop kennt Notifications nicht.

`FlurNetz.Modules.Notifications.Contracts` bleibt in V1 leer. Andere Fachmodule rufen
Notifications nicht synchron auf; die erste fachliche Anbindung läuft ausschließlich über den
expliziten Integration-Event-Consumer im Worker.

Die Implementierung referenziert nur `Notifications.Contracts`, `Identity.Contracts`,
`Shop.Contracts`, `FlurNetz.BuildingBlocks`, `FlurNetz.Persistence` und `FlurNetz.Messaging`.
Insbesondere gibt es keine Referenz auf `FlurNetz.Modules.Shop` oder `FlurNetz.Modules.Identity`,
keine API-/Worker-Referenz, kein Cross-Module-SQL und keine Cross-Module-Foreign-Keys.

## Domainmodell und historische Snapshots

`NotificationId` ist eine implementation-owned, immutable Guid-basierte ID; `Guid.Empty` ist
ungültig. `CommunityNotification` enthält:

- `NotificationId` und `CommunityIdentityId`;
- den kanonischen `NotificationType`- und `Title`-Snapshot;
- die optionale kanonische `Message`;
- die optionale kanonische `NotificationSourceReference` aus `SourceType` und `SourceId`;
- `CreatedAtUtc` sowie das optionale `ReadAtUtc`.

Type, Title, Message und SourceReference werden beim Erzeugen validiert und als Snapshot
gespeichert. Beim Lesen werden keine Shop-, Inventory- oder Economy-Tabellen abgefragt und kein
Text nachträglich aus fremden Daten zusammengesetzt. Dadurch bleiben alte Notifications nach
späteren Änderungen an Shop-Angeboten unverändert. Texte sind getrimmt, U+0000-frei,
wohlgeformtes UTF-16 und nach Unicode-Skalarwerten auf 100/200/2000 Zeichen begrenzt.

Beide Teile einer SourceReference sind gemeinsam gesetzt oder gemeinsam `NULL`; ihre V1-Grenzen
sind 100 Unicode-Skalarwerte für `SourceType` und 200 für `SourceId`. Eine Referenz enthält
keine fremden Domainobjekte.

Alle fachlichen Zeitpunkte sind UTC mit Offset `00:00` und PostgreSQL-kompatibler
Mikrosekundenpräzision. Der Erstellungs-Use-Case und die Read-Mutationen verwenden die bestehende
`IClock`-Abstraktion; die Modulregistrierung überschreibt keine bereits gesetzte Clock.

## Application und Persistenz

Die Application-Schicht bietet konkrete Use Cases für Erzeugen, Einzel-Lookup, identity-isolierte
Liste, Unread Count, Mark Read, Mark Unread und Mark All Read. Die Persistenz ist ein gezielter
`CommunityNotificationStore` mit Dapper/Npgsql-SQL; es gibt kein Generic Repository.

Die erste Notifications-Migration lautet:

`Notifications:1:CreateCommunityNotifications`

Sie besitzt die Tabelle `community_notifications` mit den Snapshot-Spalten, `timestamptz(6)`
für die Zeitpunkte und einer PostgreSQL-Constraint für die gemeinsame NULL-Semantik der
SourceReference. Die Inbox-Abfrage verwendet den Index
`(community_identity_id, created_at_utc DESC, id DESC)`. Für die tatsächlich verwendete
Unread-Abfrage existiert zusätzlich ein entsprechender partieller Index auf
`read_at_utc IS NULL`.

Es existieren keine Foreign Keys zu Identity, Shop oder einem anderen Modul. Ein normaler Insert
öffnet und committed seine eigene Transaktion. Der zusätzliche transaction-aware Insert erhält
`DbConnection` und `DbTransaction` vom Aufrufer, führt keinen Commit aus und ist dadurch für die
Messaging-Transaktion geeignet.

## Read-/Unread-Lebenszyklus

`ReadAtUtc == NULL` bedeutet ungelesen. Mark Read setzt den aktuellen kanonischen UTC-Zeitpunkt;
ein erneuter Aufruf erhält den ersten Read-Zeitpunkt. Mark Unread setzt auf `NULL` zurück und ist
ebenfalls idempotent. Mark All Read betrifft nur die angegebene Identity, aktualisiert nur bisher
ungelesene Zeilen und verwendet für den gesamten Aufruf denselben Zeitpunkt. Eine leere Inbox
liefert dabei korrekt `0`.

Alle Einzel- und Mutationszugriffe der HTTP-Inbox enthalten die `CommunityIdentityId` in der
Query. Ein fremdes oder unbekanntes Objekt wird daher als nicht gefunden behandelt und nicht als
fremde Ressource offengelegt.

## Inbox-Pagination

Die Liste ist newest-first mit der verbindlichen Reihenfolge
`created_at_utc DESC, id DESC`. Sie verwendet Keyset-Pagination und liest `pageSize + 1` Zeilen,
um `NextCursor` ohne zusätzlichen Count zu bestimmen. `pageSize` hat Default 50 und den Bereich
1 bis 100. Der opaque API-Cursor enthält Version, Identity, `unreadOnly`, `createdAtUtc` und
`notificationId`; er ist strikt Base64Url-/UTF-8-/JSON-validiert und an Identity und Filter
gebunden. Offset-Pagination und ein unnötiger Count für normale Seiten werden nicht verwendet.

## Shop-Purchase-Consumer

Der Worker registriert `ShopPurchaseCompletedIntegrationEvent` aus `Shop.Contracts` mit
`shop.purchase-completed` v1 und der stabilen Consumer Identity
`notifications.shop-purchase`. Die explizite Policy erzeugt für `event.CommunityIdentityId`
eine Notification mit:

- Type: `shop.purchase-completed`;
- Title: `Shop-Kauf abgeschlossen`;
- Message: `Dein Shop-Kauf wurde erfolgreich abgeschlossen.`;
- SourceType: `shop.purchase`;
- SourceId: die kanonische `ShopPurchaseId`-Darstellung.

Die Policy verwendet ausschließlich die Event-Payload und stabile Notifications-eigene Texte.
Sie ruft keinen Shop-Store, keine Shop-Implementierung und keine fremde Tabelle auf. Der
Purchase selbst bleibt vollständig Shop-owned; die Notification entsteht eventual nach dem
erfolgreichen Shop-Commit.

## Messaging-Atomicity und Idempotenz

Der vorhandene `OutboxProcessor` reserviert für jeden Consumer zuerst den Inbox-Eintrag und ruft
den Handler in derselben PostgreSQL-Connection und -Transaction auf. Der Notifications-Handler
verwendet genau diese Transaction für den Notification-Insert. Bei Erfolg werden Inbox und
Notification gemeinsam committed; bei einem Handler- oder Persistenzfehler werden beide
zurückgerollt und die Outbox kann über die vorhandene Retry-/Poison-Semantik erneut verarbeitet
werden. Eine zusätzliche Notifications-Deduplizierungstabelle gibt es nicht.

Eine Duplicate Delivery für dieselbe Message und `notifications.shop-purchase` erzeugt wegen der
Messaging-Inbox höchstens eine Notification. Die Outbox ist kein Event Store und kein Replay-Log:
bereits früher ohne diesen Consumer erfolgreich verarbeitete Shop-Events werden nicht
nachträglich erneut zugestellt. Ein historischer Shop-Backfill ist in V1 ausdrücklich nicht
implementiert.

## Worker und API

`FlurNetz.Worker` lädt `NotificationsModule`, die Notifications-Migration und den konkreten
Consumer. Er lädt weiterhin weder die Shop-Implementierung noch Shop-Migrationen. Der API-Host
lädt nur `AddNotificationsModule()` für die Inbox-Use-Cases und die gemeinsame
Notifications-Migration; er startet keinen `OutboxProcessor`, keinen Messaging-Worker und keinen
Notifications-Consumer.

Die persönliche HTTP-Grenze verwendet API-eigene DTOs:

```text
GET  /api/identities/{communityIdentityId}/notifications?pageSize={pageSize}&cursor={cursor}&unreadOnly={bool}
GET  /api/identities/{communityIdentityId}/notifications/unread-count
GET  /api/identities/{communityIdentityId}/notifications/{notificationId}
POST /api/identities/{communityIdentityId}/notifications/{notificationId}/read
POST /api/identities/{communityIdentityId}/notifications/{notificationId}/unread
POST /api/identities/{communityIdentityId}/notifications/read-all
```

Malformed GUIDs, ungültige Seitengrößen, ungültige Boolean-Filter und malformed, fremde oder
filterfremde Cursor liefern `400 ProblemDetails`. Unbekannte oder identity-fremde Notifications
liefern `404`. Es gibt bewusst keinen öffentlichen Create-Endpunkt. Authentifizierung und
Authorization sind noch keine Security Foundation des Gesamtsystems und müssen vor produktivem
Einsatz separat ergänzt werden.

## Tests und V1-Grenze

Die Unit- und Architekturtests prüfen Domaininvarianten, Unicode, Zeitkanonisierung,
Read-/Unread-Idempotenz, Use Cases, Cursorbindung, DI-Scope, Contract-Minimalität und
Abhängigkeitsrichtung. Das eigene `FlurNetz.Modules.Notifications.IntegrationTests` verwendet
echtes PostgreSQL und prüft Migration/History/Checksum, Constraints, Snapshot-Roundtrip,
Identity-Isolation, Sortierung, mehrseitige Pagination, Unread-Lifecycle, transaction-aware
Commit/Rollback und SourceReference.

Worker-/Messaging- und Workflow-Tests prüfen Migration, Consumer-Registrierung, den realen
Shop-Purchase-zu-Outbox-zu-Processor-zu-Notification-Weg, Inbox-Deduplizierung und die
Weiterfunktion des Progression-Workflows. API-Integrationstests prüfen die echte HTTP-Grenze,
DTO-Abbildung, Cursor, Filter, Identity-Isolation und Mutationen.

Nicht Teil von Notifications V1 sind externe Zustellkanäle (E-Mail, Discord, Twitch, YouTube,
Kick, Push, SMS), Provider und Delivery Queues, Preferences, Quiet Hours, Digest, Realtime,
Templates/Localization, generische Rule-/Event-Mapping-Engines, Delete/Archive/Retention/
Expiration, Backfill, neue Shop-Event-Versionen, Notification-Events nach außen, Distributed
Transactions, Sagas und ein Generic Repository.
