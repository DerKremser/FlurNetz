# FlurNetz.Worker

## Rolle

FlurNetz.Worker ist ein ausführbarer .NET-10-Generic-Host und eine reine Composition Root
für die Messaging-Laufzeit. Der Host startet keinen HTTP-Server und enthält keine
Fachlogik, Domain-Entities, fachlichen Events, XP-Regeln oder eigene Outbox-/Inbox-SQL-
Operationen.

Der Worker referenziert ausschließlich:

- FlurNetz.Messaging für Registry, Serializer, Migration und OutboxProcessor;
- FlurNetz.Persistence für PostgreSQL-Konfiguration, Connection Factory und MigrationRunner;
- FlurNetz.Modules.Progression für den vorhandenen Consumer, Store und die Progression-Migration;
- FlurNetz.Modules.Notifications für den persönlichen Inbox-Store, die Migration und den
  `shop.purchase-completed`-Consumer;
- FlurNetz.Modules.Engagement.Contracts für den bereits bestehenden Event-Vertrag;
- FlurNetz.Modules.Shop.Contracts für den bekannten `shop.purchase-completed`-Vertrag.

Die Engagement-Implementierung, Identity-Implementierung, Shop-Implementierung, API und alle übrigen Fachmodule
werden nicht geladen. Insbesondere referenziert der Worker nicht `FlurNetz.Modules.Shop` und
zieht keine Shop-Implementierung oder Shop-Migration nach. Der Worker erzeugt keine
Engagement-Aktivitäten; Engagement bleibt der Besitzer des Events engagement.message-recorded.

Der API-Host ist im Shop-Slice der getrennte Producer: Sein
`POST /api/shop/offers/{offerId}/purchases` führt den bestehenden atomaren Purchase aus und
hinterlässt `shop.purchase-completed` v1 zunächst `pending` in der PostgreSQL-Outbox. Der
API-Prozess startet dafür keinen Processor und keinen Consumer. Dieser Worker bleibt die
separate Processor-Runtime, die die Nachricht später verarbeitet; der Notifications-Consumer
ist im Worker registriert.

## PostgreSQL und Startup

Die PostgreSQL-Verbindung wird ausschließlich aus ConnectionStrings:FlurNetz gelesen. Fehlt
der Wert oder ist er ungültig, schlägt die Erstellung der bestehenden
PostgreSqlConnectionFactory früh fehl und der Host startet nicht. Zugangsdaten werden über
User Secrets, Umgebungsvariablen oder eine andere normale .NET-Konfigurationsquelle
bereitgestellt und nicht in Repository-Dateien versioniert.

Vor dem Start des Processing-Loops führt der Worker den bestehenden MigrationRunner aus.
Registriert werden genau die Migrationsquellen der Messaging Foundation, des Progression- und
des Notifications-Moduls:

- Messaging:1:CreateOutboxAndInbox;
- Progression:1:CreateCommunityProgressions;
- Notifications:1:CreateCommunityNotifications.

EngagementMigrationSource wird bewusst nicht registriert. Der Worker benötigt
engagement_activities für das Consuming nicht. Ebenso werden `Shop:1:CreateShopOffers` und
`Shop:2:CreateShopPurchases` nicht registriert; `shop_offers`, `shop_purchases`,
`shop_purchase_requests` und `shop_purchase_guards` gehören nicht zur Worker-Runtime. Schlägt
eine Migration fehl, wird der Fehler kritisch geloggt und der Hoststart abgebrochen; es gibt
keinen endlosen Migration-Retry im BackgroundService.

Anschließend validiert der Startup-Service die reale Komposition. Die Registry enthält den
Event-Typ `engagement.message-recorded` v1 explizit über
`MessageEngagementRecordedIntegrationEvent.MessageType` und
`MessageEngagementRecordedIntegrationEvent.SchemaVersion` sowie
`shop.purchase-completed` v1 über `ShopPurchaseCompletedIntegrationEvent.MessageType` und
`ShopPurchaseCompletedIntegrationEvent.SchemaVersion`. Es gibt kein Assembly Scanning.
`IntegrationEventJsonSerializer` verwendet dieselbe Singleton-Registry wie der
`OutboxProcessor`. Die Progression-Consumer-Registration, das bewusste Fehlen eines Shop-
Consumers, der Notifications-Consumer und der vollständig auflösbare `OutboxProcessor` werden
vor dem Loop geprüft.

## Runtime-Verarbeitung

MessagingWorker ist ein kleiner BackgroundService ohne fachliche Entscheidungen. Jeder
Processing-Lauf erzeugt über IServiceScopeFactory einen neuen asynchronen DI-Scope, löst den
scoped OutboxProcessor auf und ruft ProcessBatchAsync auf. Dadurch werden Progression-Handler,
Progression-Store und Processor nicht dauerhaft durch den Singleton-Worker festgehalten.

Nach einem leeren Batch wartet der Host standardmäßig eine Sekunde. Bei mindestens einer
geclaimten Nachricht wird ohne unnötige lange Pause der nächste Batch gestartet, damit ein
Backlog zügig abgearbeitet wird. Unerwartete technische Fehler eines gesamten Batch-Aufrufs
werden strukturiert geloggt; danach wartet der Host standardmäßig fünf Sekunden und versucht
weiter. IdleDelay und FailureDelay sind unter MessagingWorker konfigurierbar und müssen
größer als null sein.

BatchSize, MaxAttempts, RetryDelay und LeaseDuration gehören weiterhin zu
OutboxProcessingOptions. Message-Level-Retry, Failed-/Poison-Status, Inbox-Deduplizierung,
Leases und Lease-Recovery bleiben vollständig Verantwortung des vorhandenen
OutboxProcessor. Der Worker führt kein zweites Retry-System und keine Claim-/Lease-Cleanup-
Logik ein.

Der reale Workflow lautet:

API- oder anderer Producer → PostgreSQL-Outbox → FlurNetz.Worker → OutboxProcessor → Consumer.

Eine Nachricht engagement.message-recorded mit Schema-Version 1 wird vom bestehenden
Progression-Consumer als genau 1 XP interpretiert. Inbox-Eintrag und XP-Write bleiben in der
vom Processor bereitgestellten PostgreSQL-Transaktion atomar.

Eine gültige `shop.purchase-completed`-Nachricht wird nach erfolgreicher Deserialisierung durch
den Notifications-Consumer verarbeitet. Notification-Insert und Inbox-Eintrag verwenden die
gleiche Processor-Transaktion; ein Fehler rollt beide zurück und die Outbox bleibt für Retry-
beziehungsweise Poison-Behandlung zuständig. Bereits verarbeitete Outbox-Nachrichten werden
nicht zu einem späteren Replay-Log für neu hinzukommende Consumer.

## Shutdown und Betriebsindikatoren

Der Worker prüft das CancellationToken vor jedem neuen Batch, übergibt es an den Processor
und verwendet es auch für Idle- und Failure-Delays. Eine Cancellation während des
Host-Shutdowns wird nicht als Fehler geloggt. Der BackgroundService beendet sich sauber,
beginnt keine neuen Batches und hinterlässt die Transaktionssemantik beim Processor und den
Consumer-Transaktionen.

Geloggt werden Worker-Startup, erfolgreiche Migrationen, erfolgreiche Kompositionsvalidierung,
der Start und das Ende des Processing-Loops, tatsächliche Batch-Ergebnisse sowie unerwartete
technische Batch-Fehler und der Shutdown. Leere Polls erzeugen kein Info-Log. Connection
Strings, Secrets und vollständige Event-Payloads werden nicht standardmäßig geloggt.

Ein gestarteter Processing-Loop bedeutet, dass Konfiguration, erforderliche Migrationen und
Registry-/Handler-Komposition erfolgreich aufgebaut beziehungsweise validiert wurden. Dafür
ist kein separater Readiness-Service erforderlich. Der Worker besitzt in diesem Schritt
keinen Health- oder Management-Endpunkt, kein Kestrel, keinen HTTP-Port und keine
Plattformintegration.

## Tests

FlurNetz.Worker.IntegrationTests startet den echten Generic Host gegen PostgreSQL über
Testcontainers oder FLURNETZ_TEST_CONNECTION_STRING. Die Tests prüfen Startup-Migrationen,
das Ausbleiben der Engagement-Tabelle, den leeren Queue-Zustand, ein nach dem Startup
eingereihtes Engagement-Event ohne direkten ProcessBatchAsync-Aufruf, genau 1 XP, processed
Outbox, Inbox-Deduplizierung für den Progression-Consumer, ein nach dem Startup eingereihtes
`shop.purchase-completed` mit genau einer Notification und einem Inbox-Eintrag, das Ausbleiben
aller Shop-Fachtabellen, kontinuierliches Polling und Graceful Shutdown. Der separate
Notifications-PostgreSQL-Test prüft zusätzlich transaction-aware Commit/Rollback und die
fachliche Inbox.
