# Messaging Foundation

`FlurNetz.Messaging` ist die technische Grundlage für Kommunikation zwischen FlurNetz-
Modulgrenzen. Der erste vollständige Producer/Consumer-Workflow verbindet Engagement und
Progression über Outbox, Processor und Inbox. Der Shop-Purchase-Slice ist der zweite reale
Outbox-Produzent und veröffentlicht `shop.purchase-completed` v1 atomar mit dem Kauf.
Die Foundation bleibt fachlich neutral und kennt weder die Module noch deren Contracts.

## Domain Events und Integration Events

Domain Events (`IDomainEvent`) sind interne Prozesssignale. Der `DomainEventDispatcher` liefert sie in expliziter Registrierungsreihenfolge sequenziell an passende Handler. Ein fehlender Handler ist ein No-op, der erste Handler-Fehler wird nicht verschluckt und Cancellation wird weitergereicht. Domain Events werden weder serialisiert noch in der Outbox gespeichert.

Integration Events (`IIntegrationEvent`) sind dagegen stabile technische Nachrichten an einer Modulgrenze. Ihre fachliche Payload bleibt von technischen Metadaten getrennt. Sie werden nicht sofort an einen externen Handler gesendet: `EnqueueAsync` bedeutet zunächst durable Persistierung in der Outbox.

## Envelope, Identität und Versionierung

`IntegrationEventEnvelope` enthält:

- `MessageId`: echte, stabile eindeutige Identität für Outbox, Inbox, Logs und Wiederzustellung;
- `MessageType`: expliziter logischer Typname, beispielsweise `identity.user-created`;
- `SchemaVersion`: positive, explizite Payload-Version;
- `OccurredAtUtc` sowie optional `CorrelationId` und `CausationId`.

Die `IntegrationEventTypeRegistry` wird ausdrücklich durch spätere Module registriert. Sie ordnet die Kombination aus logischem Typ und Version einem CLR-Typ zu, erkennt doppelte Registrierungen und lehnt unbekannte Typen oder Versionen mit einem klaren Fehler ab. Es gibt keine Assembly-Suche und kein `AssemblyQualifiedName` im Wire-Format; Refactorings und Assembly-Versionen verändern dadurch keine persistierte Nachrichtenidentität.

`IntegrationEventJsonSerializer` verwendet ausschließlich `System.Text.Json` und UTF-8. Die Registry entscheidet vor der Deserialisierung über den erlaubten CLR-Typ. Beliebige polymorphe CLR-Typen aus JSON werden nicht aktiviert.

## PostgreSQL-Outbox

`PostgreSqlOutboxPublisher` erhält eine bereits geöffnete `PostgreSqlTransaction`. Er öffnet keine zweite Verbindung und führt keinen eigenen Commit aus. Ein fachlicher Write und der Outbox Insert können deshalb so aussehen:

1. PostgreSQL-Transaktion beginnen;
2. fachlichen Datenbank-Write auf `transaction.Connection` ausführen;
3. Integration Event über `EnqueueAsync` in `flurnetz_messaging.outbox_messages` schreiben;
4. dieselbe Transaktion committen oder zurückrollen.

Der Commit macht beide Änderungen dauerhaft sichtbar. Ein Rollback hinterlässt weder den fachlichen Write noch die Outbox-Nachricht. Die technische Tabelle speichert MessageId, logischen Typ, Schema-Version, JSON-Payload, UTC-Zeitpunkte, optionale Korrelation/Ursache, Status, Versuchszähler, Lease-Informationen und einen gekürzten letzten Fehlertext.

## Processor, Claiming und Lebenszyklus

Der `OutboxProcessor` ist host-unabhängig und führt mit `ProcessBatchAsync` genau einen aufrufbaren Batch-Lauf aus. Er startet keine Endlosschleife und ist kein `BackgroundService`. Der API-Host, `FlurNetz.Worker` oder ein Testhost entscheidet über den Aufrufzeitpunkt.

Offene Outbox-Nachrichten werden in einer kurzen PostgreSQL-Transaktion mit `FOR UPDATE SKIP LOCKED` ausgewählt und über `locked_until_utc` geleast. Dabei wird der Versuchszähler atomar erhöht. Ein abgestürzter Processor blockiert eine Nachricht nur bis zum Ablauf des Leases; ein weiterer Lauf kann sie danach erneut übernehmen.

Ein bekannter und erfolgreich deserialisierter Eventtyp gilt auch ohne passenden registrierten
Consumer als erfolgreich verarbeitet. In diesem Fall wird kein Handler ausgeführt, kein
Inbox-Eintrag geschrieben und die Outbox-Nachricht anschließend als `processed` markiert. Der
Fall erzeugt weder ein Retry noch eine Poison-/Failed-Nachricht. Unbekannte Message Types oder
Schema-Versionen bleiben dagegen normale Verarbeitungsfehler und durchlaufen weiterhin die
Retry-/Poison-Regeln.

Die Outbox liefert an die zum Verarbeitungszeitpunkt registrierten Consumer. Sie ist kein Event
Store und kein dauerhaftes Replay-Log für Consumer, die erst später hinzukommen; historische
Backfills sind eine separate Anforderung.

## Inbox und transactional Inbox

`flurnetz_messaging.inbox_messages` besitzt den Schlüssel aus stabiler `consumer_name`-Identität und `message_id`. Ein Consumer wird ausdrücklich benannt und nicht dauerhaft über seinen CLR-Klassennamen identifiziert.

Vor dem Handler wird der Inbox-Eintrag in derselben PostgreSQL-Transaktion eingefügt. `ON CONFLICT DO NOTHING` erkennt eine bereits erfolgreich verarbeitete Zustellung. Bei einem neuen Eintrag führt der Handler seinen Business Write über den `IntegrationEventHandlerContext` auf derselben Connection und Transaction aus. Erst der gemeinsame Commit bestätigt Business Effect und Inbox-Markierung. Wirft der Handler einen Fehler, rollt die Transaktion beides zurück; die Nachricht bleibt retrybar.

Verschiedene Consumer können dieselbe MessageId jeweils einmal verarbeiten. Bei Duplicate Redelivery überspringt die Inbox den bereits erfolgreichen Consumer, während die Outbox-Nachricht kontrolliert abgeschlossen werden kann.

## Retry und Poison Messages

Fehler werden technisch knapp als Exception-Typ bzw. sicherer Registry-Hinweis gespeichert; vollständige Fehlermeldungen, Payloads und Secrets gehören nicht in `last_error` oder die technischen Logs. Bis `MaxAttempts` erreicht ist, wird eine Nachricht mit `next_attempt_at_utc` und einer einfachen Verzögerung zurückgestellt. Die Zeitplanung verwendet `IClock`.

Nach dem letzten erlaubten Versuch erhält die Nachricht den Status `failed` (Poison). Sie wird nicht erneut ausgewählt und blockiert keine späteren Nachrichten. Unbekannte logische Typen oder Versionen gelten als normale Verarbeitungsfehler und durchlaufen dieselben Retry-/Poison-Regeln.

## Foundation und Runtime Host

Die Messaging Foundation definiert nur die technischen Outbox-/Inbox- und Processor-
Verträge. Sie startet keinen Prozess und kennt weder Progression noch andere Fachmodule.
`FlurNetz.Worker` ist der erste separate Runtime-Host: Er registriert die benötigten
Contracts und Consumer explizit, führt die Messaging- und Progression-Migrationen beim
Startup aus und ruft danach `OutboxProcessor.ProcessBatchAsync` kontinuierlich auf. Seine
Registry enthält `engagement.message-recorded` v1 und `shop.purchase-completed` v1; dafür
referenziert er `FlurNetz.Modules.Shop.Contracts`, nicht die Shop-Implementierung. Für das
Shop-Event ist aktuell bewusst kein Consumer registriert, sodass die oben beschriebene
consumerlose Erfolgssemantik greift.
Die Worker-spezifischen Idle-/Failure-Delays und der Scope pro Batch gehören zum Host, nicht
zur Foundation. Message-Level-Retry und Lease-Semantik bleiben beim Processor.

## API als Producer-Host

Der API-Host besitzt für den HTTP-Purchase eine bewusst schmale Producer-Runtime. Er registriert
explizit ausschließlich `ShopPurchaseCompletedIntegrationEvent` mit den Contract-Konstanten
`MessageType = "shop.purchase-completed"` und `SchemaVersion = 1`. Dazu kommen genau die
`IntegrationEventTypeRegistry`, `IntegrationEventJsonSerializer`,
`IIntegrationEventSerializer`, `IIntegrationEventPublisher` als
`PostgreSqlOutboxPublisher` sowie `MessagingMigrationSource`. Die `IClock`-Instanz kommt aus
dem `AddShopModule()`-Wiring.

Der API-Prozess registriert keinen `OutboxProcessor`, keinen Messaging-Worker, keinen
Hosted-Service, keinen Inbox-Handler und keinen Shop-Consumer. Ein erfolgreicher
`POST /api/shop/offers/{offerId}/purchases` schreibt das unveränderte
`shop.purchase-completed` v1 als `pending` in die Outbox; der separate Worker ist für die
spätere Verarbeitung zuständig.

## Migrationen und Tests

`MessagingMigrationSource` registriert die technischen Tabellen unter dem eindeutigen Migration-Owner `Messaging` beim vorhandenen SQL-first `MigrationRunner`. Es gibt keine fachlichen Migrationen.

Die Unit Tests prüfen Domain-Dispatcher, Registry und Serialisierung. Architecture Tests sichern Namespace, Abhängigkeitsrichtung, fachliche Neutralität und das Fehlen generischer Repositories. Die PostgreSQL-Integrationstests verwenden Testcontainers und prüfen Migration/Idempotenz, atomaren Commit und Rollback, Processor, Inbox-Deduplizierung, transactional Inbox, Retry, Poison, unbekannte Typen, Duplicate Redelivery, bekannte Eventtypen ohne Consumer und paralleles Claiming. Die Worker-Integrationstests prüfen zusätzlich die echte Host-Schleife, Startup-Migrationen, Verarbeitung von Engagement nach dem Hoststart, die consumerlose Verarbeitung von `shop.purchase-completed` ohne Inbox-Eintrag und Graceful Shutdown. SQLite und In-Memory-Datenbanken werden nicht verwendet.

## Erster fachlicher Workflow

Der erste Workflow verwendet die Foundation host-unabhängig im E2E-Test:

`RecordMessageEngagement → Activity + Outbox → engagement.message-recorded v1 → OutboxProcessor → Progression Inbox → 1 XP`

Engagement besitzt und veröffentlicht die Tatsache `MessageEngagementRecordedIntegrationEvent`.
Der Contract enthält ausschließlich die interne `CommunityIdentityId`; insbesondere keine XP,
Message-Texte oder Plattformdaten. Activity- und Outbox-Write teilen eine Transaktion.

Progression registriert den Eventtyp explizit und konsumiert ihn mit der stabilen Consumer
Identity `progression.message-engagement-xp`. Der Handler verwendet die Inbox-Transaktion für
den transaction-aware Progression-Grant. Dadurch sind Inbox-Eintrag und XP-Write atomar, ein
Consumer-Fehler bleibt retrybar und Duplicate Delivery erzeugt keinen zweiten fachlichen
Effekt. Schema v1 bleibt durch den stabilen logischen Message Type und die explizite Registry
von CLR-Refactorings unabhängig.

Der Workflow-Test führt `OutboxProcessor.ProcessBatchAsync(...)` weiterhin direkt aus,
während `FlurNetz.Worker` denselben Foundation-Processor außerhalb von Tests
kontinuierlich betreibt.

## Shop-Purchase als zweiter Outbox-Produzent

Der atomare Shop-Purchase erzeugt nach erfolgreicher Identity-Prüfung, Economy-Abbuchung,
Inventory-Vergabe und Purchase-Persistenz ein
`ShopPurchaseCompletedIntegrationEvent` mit dem stabilen logischen Typ
`shop.purchase-completed` und Schema-Version `1`.

Der `PostgreSqlShopPurchaseExecutor` verwendet den bestehenden
`IIntegrationEventPublisher` innerhalb derselben `PostgreSqlTransaction`. Damit werden
Request-Reservation, Economy-Debit, Inventory-Grant, Shop-Purchase und Outbox-Nachricht durch
denselben Commit sichtbar. Ein Fehler vor dem Commit hinterlässt auch keine Outbox-Nachricht.

Die `ShopPurchaseRequestId` gehört nicht in die Event-Payload; sie wird als technische
`CorrelationId` des Envelopes verwendet. Die fachliche Payload enthält ausschließlich den
unveränderlichen Kauf-Snapshot.

Der Worker macht den Eventtyp explizit bekannt, ergänzt aber bewusst keinen Consumer.
Der Worker verarbeitet eine gültige `shop.purchase-completed`-Nachricht deshalb erfolgreich
ohne Inbox-Eintrag. Ein späterer echter Consumer muss seinen Eventtyp und seine stabile
Consumer-Identity ausdrücklich registrieren und kann dann die vorhandene Inbox-/Retry-
Infrastruktur verwenden; bereits verarbeitete Outbox-Nachrichten werden dadurch nicht
rückwirkend replaybar.
