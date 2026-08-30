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
- FlurNetz.Modules.Engagement.Contracts für den bereits bestehenden Event-Vertrag.

Die Engagement-Implementierung, Identity-Implementierung, API und alle übrigen Fachmodule
werden nicht geladen. Der Worker erzeugt keine Engagement-Aktivitäten; Engagement bleibt der
Besitzer des Events engagement.message-recorded.

## PostgreSQL und Startup

Die PostgreSQL-Verbindung wird ausschließlich aus ConnectionStrings:FlurNetz gelesen. Fehlt
der Wert oder ist er ungültig, schlägt die Erstellung der bestehenden
PostgreSqlConnectionFactory früh fehl und der Host startet nicht. Zugangsdaten werden über
User Secrets, Umgebungsvariablen oder eine andere normale .NET-Konfigurationsquelle
bereitgestellt und nicht in Repository-Dateien versioniert.

Vor dem Start des Processing-Loops führt der Worker den bestehenden MigrationRunner aus.
Registriert werden genau die Migrationsquellen der Messaging Foundation und des
Progression-Moduls:

- Messaging:1:CreateOutboxAndInbox;
- Progression:1:CreateCommunityProgressions.

EngagementMigrationSource wird bewusst nicht registriert. Der Worker benötigt
engagement_activities für das Consuming nicht. Schlägt eine Migration fehl, wird der Fehler
kritisch geloggt und der Hoststart abgebrochen; es gibt keinen endlosen Migration-Retry im
BackgroundService.

Anschließend validiert der Startup-Service die reale Komposition. Die Registry enthält den
Event-Typ explizit über MessageEngagementRecordedIntegrationEvent.MessageType und
MessageEngagementRecordedIntegrationEvent.SchemaVersion. Es gibt kein Assembly Scanning.
IntegrationEventJsonSerializer verwendet dieselbe Singleton-Registry wie der
OutboxProcessor. Die Progression-Consumer-Registration und der vollständig auflösbare
OutboxProcessor werden vor dem Loop geprüft.

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

Producer → PostgreSQL-Outbox → FlurNetz.Worker → OutboxProcessor → Progression-Consumer.

Eine Nachricht engagement.message-recorded mit Schema-Version 1 wird vom bestehenden
Progression-Consumer als genau 1 XP interpretiert. Inbox-Eintrag und XP-Write bleiben in der
vom Processor bereitgestellten PostgreSQL-Transaktion atomar.

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
eingereihtes Event ohne direkten ProcessBatchAsync-Aufruf, genau 1 XP, processed Outbox,
Inbox-Deduplizierung für den Progression-Consumer, kontinuierliches Polling und Graceful
Shutdown.
