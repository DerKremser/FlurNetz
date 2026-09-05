# Architekturübersicht

Integrations V1 ist inzwischen der erste vollständige External-Identity-Mapping- und
Resolution-Slice mit eigener PostgreSQL-Tabelle, Identity-Existenzprüfung über den
öffentlichen Contract und interner HTTP-Management-Grenze. Die vollständige Beschreibung
steht in [integrations.md](integrations.md).

Automation V1 ergänzt den ersten betreiberkonfigurierbaren, persistierten Rule-Engine-Slice.
Die Engine verarbeitet ausschließlich engagement.message-recorded und shop.purchase-completed,
projiziert beide Events auf einen eigenen Snapshot und führt passende Rules deterministisch
über Conditions und Actions aus. AutomationExecution, Economy-Credit, Notification-Create und
Messaging-Inbox teilen die vorhandene PostgreSQL-Transaktion. Management bleibt im API-Host,
Runtime bleibt im Worker; die vollständige Beschreibung steht in
[automation.md](automation.md).

FlurNetz wird modular aufgebaut. Die physischen Grenzen der vorgesehenen Fachmodule sind jetzt als getrennte Contracts- und Implementierungs-Assemblies angelegt. Identity ist das erste Modul mit einem bewusst minimalen fachlichen Vertical Slice und einer API; Engagement kann eine normalisierte Message-Aktivität gemeinsam mit einer Outbox-Nachricht persistieren; Progression konsumiert diese Nachricht und vergibt atomar 1 XP; Economy persistiert atomare Community-Salden mit PostgreSQL-Zeilensperren; Rewards besitzt nun ein minimales persistiertes und ausführbares Domainmodell für Reward Definitions, Packages, Sources und Grant-Records; Inventory besitzt den ersten persistierten Vertical Slice für mengenbasierte Community-Bestände; Titles besitzt persistierten Community-State und einen unabhängigen Definitionskatalog; Shop besitzt den atomaren Inventory-Purchase-Slice; Notifications besitzt eine persistente persönliche In-App-Inbox und konsumiert den Shop-Purchase-Event; Automation besitzt eine persistierte Rule Engine für beide Events. Die übrigen noch nicht begonnenen Fachmodule enthalten keine Fachlogik, fachlichen Entities, Tabellen oder konkreten Events. Mit `FlurNetz.Api` und `FlurNetz.Worker` besitzt FlurNetz zwei unabhängige ausführbare Hosts. Fachmodule greifen nicht auf fremde Implementierungen oder Tabellen zu; Cross-Module-Komposition erfolgt über öffentliche Capabilities und gemeinsame technische Transaktionen, übrige Kommunikation über öffentliche Contracts und Integration Events.

Identity bildet mit `CommunityIdentityId` und der minimalen `CommunityIdentity` die zentrale interne Identität eines Community-Mitglieds. Der erste Slice erzeugt diese Identität, persistiert sie in der Identity-eigenen PostgreSQL-Tabelle und lädt sie über die interne ID. Engagement nimmt eine bereits aufgelöste `CommunityIdentityId` an und persistiert intern eine Message-Aktivität mit UTC-Zeitpunkt; es fragt Identity nicht ab und verwendet keinen Cross-Module-Foreign-Key. Externe Plattformkennungen werden an der Integrations-Resolution-Grenze aufgelöst und ersetzen die interne Identität nicht. Das Mapping ist provider- und ID-opaque, besitzt keinen Cross-Module-Foreign-Key und erzeugt bei unbekannten IDs keine neue Community-Identität. Persistence und Messaging werden als getrennte technische Infrastruktur aufgebaut; echte Plattformverbindungen werden weiterhin über spätere externe Adapter integriert.

Progression hält mit `CommunityProgression` den aktuellen XP-Wert einer einzelnen
`CommunityIdentityId`. Der erste persistierte Slice startet mit `0` XP, erzeugt den Zustand
lazy beim ersten Grant und unterstützt positive, überlaufsichere Akkumulation. Die atomare
Read/Modify/Write-Operation verwendet PostgreSQL-Zeilensperren gegen Lost Updates. Der erste
Engagement→Progression-Workflow läuft über Outbox und Inbox; weder Level-Logik noch
Rewards-Ausführung oder API gehören zu diesem Progression-Slice.

Economy hält mit `CommunityEconomy` den neutralen Economy-Saldo genau einer internen
`CommunityIdentityId`. `EconomyBalance` ist immutable, auf `long` basierend und nicht-negativ;
Gutschriften und Abbuchungen akzeptieren ausschließlich positive Beträge, schützen vor Overflow
und verhindern eine Überziehung. Der Zustand wird lazy bei der ersten erfolgreichen Gutschrift
angelegt. Der interne Store führt Credits und Debits atomar mit `SELECT FOR UPDATE` aus; ein
fehlgeschlagener Debit auf einen fehlenden Zustand erzeugt keine Zeile. Eine konkrete Währungsbezeichnung, Multi-Currency, Transfers, Ledger und Economy-API sind
weiterhin nicht Bestandteil dieses Economy-Slices. `Economy.Contracts` bietet inzwischen
schmale transaction-aware Credit- und Debit-Fähigkeiten; Rewards verwendet Credit und der
Shop-Purchase Debit. Economy kennt deren Aufrufer nicht und behält Domain-, Lock- und
Tabellenownership selbst.

Rewards beschreibt mit `RewardDefinition` und dem ersten konkreten Typ
`EconomyBalanceRewardDefinition` eine Economy-Balance-Gutschrift. Definitionen und
verpflichtende Packages werden persistiert; `GrantRewardPackage` reserviert eindeutige
`RewardGrant`-Records und führt Economy-Writes über eine gemeinsame PostgreSQL-Transaktion
all-or-nothing aus. `RewardSource` und `RewardDefinitionId` bilden die technische Grenze
`SourceType + SourceId + RewardDefinitionId`; ein Duplicate ist ein idempotenter No-op,
Partial-State ein Fehler. `Rewards.Contracts` bleibt leer; XP bleiben vollständig
Progression-owned. Es gibt noch keinen Runtime-Trigger, keine API- oder Worker-Anbindung.
Details stehen in [rewards.md](rewards.md).

Inventory hält mit `CommunityInventoryEntry` den Bestand genau einer `ItemDefinitionId` für
genau eine interne `CommunityIdentityId`. Der erste persistierte Slice verwendet den Composite
Key beider Kennungen, eine eigene PostgreSQL-Migration und atomare Read/Modify/Write-Operationen
mit `SELECT FOR UPDATE`. Add legt eine fehlende Position lazy an; Remove auf einer fehlenden
Position verhält sich wie Bestand null und erzeugt keine Zeile. Wird ein vorhandener Bestand
exakt auf null reduziert, löscht der Store die Zeile, sodass die Persistenz sparse bleibt.
`Inventory.Contracts` enthält `ItemDefinitionId` und die schmale transaction-aware
`IInventoryQuantityGrant`-Capability. Der Shop-Purchase verwendet diese Fähigkeit innerhalb
seiner eigenen PostgreSQL-Transaktion; Inventory kennt Shop nicht. Messaging, Reward-Ausführung,
Item-Katalog und eigene Inventory-HTTP-Endpunkte gehören weiterhin nicht zum Inventory-Slice;
die API nutzt die Grant-Capability ausschließlich im atomaren Shop-Purchase. Details stehen in
[inventory.md](inventory.md).

Titles hält mit `CommunityTitles` die freigeschalteten `TitleDefinitionId`-Werte genau einer
internen `CommunityIdentityId`. Freischaltungen sind idempotent und ändern die aktuelle Auswahl
nicht automatisch. Höchstens ein bereits freigeschalteter Titel kann aktuell ausgewählt sein;
die Auswahl kann auch vollständig geleert werden. `Unlock`, `Lock`, `SetCurrent` und
`ClearCurrent` schützen diese Domain-Invarianten; das Sperren des aktuellen Titels entfernt
zugleich die aktuelle Auswahl. `Rehydrate` lädt den Zustand über den atomaren PostgreSQL-Store
mit Root-Row-Locking und `SELECT FOR UPDATE`; drei Titles-owned Tabellen schützen zusätzlich
die Current→Unlock-Invariante. Der separate `TitleDefinition`-Katalog persistiert
DisplayName und Description in `title_definitions` über Migration V2; Katalogmutationen
verwenden `SELECT FOR UPDATE` gegen Lost Updates. `Titles.Contracts` bleibt leer. Messaging,
Rewards-, Achievement- und Shop-Anbindung sowie API und Worker bleiben außerhalb dieses Slices.
Details stehen in [titles.md](titles.md).

Shop besitzt neben `ShopOffer` und dem persistierten Angebotskatalog jetzt den ersten
atomaren Inventory-Kauf. `Shop.Contracts` veröffentlicht `ShopOfferId`,
`ShopPurchaseId`, `ShopPurchaseRequestId` und
`ShopPurchaseCompletedIntegrationEvent` mit dem stabilen Typ
`shop.purchase-completed` v1. `Shop:2:CreateShopPurchases` ergänzt Purchase-, Request- und
Guard-Persistenz; der einzige Foreign Key bleibt Shop-intern von Purchase auf Offer.

`PostgreSqlShopPurchaseExecutor` besitzt eine gemeinsame PostgreSQL-Transaktion für
Idempotenz-Reservation, Identity-Existenzprüfung, stabilen Offer-Snapshot, Kauflimit,
transaction-aware Economy-Debit, Inventory-Grant um exakt eins, Purchase-Write und Outbox.
Identische Requests erzeugen exakt einen Effekt; Fehler rollen alle Teilwirkungen gemeinsam
zurück. Shop verwendet dafür ausschließlich `Identity.Contracts`, `Economy.Contracts`,
`Inventory.Contracts`, Messaging, Persistence und BuildingBlocks, niemals fremde
Implementierungen oder Tabellen. Die API stellt Storefront-, History- und
`POST /api/shop/offers/{offerId}/purchases` sowie die getrennte
`/api/admin/shop/offers`-Management-Grenze bereit, verwendet dafür `AddShopModule()` sowie
schmale Economy-/Inventory-Capabilities. Die Management-Grenze verwendet die bestehenden
Shop-Use-Cases, führt keine neue Migration oder Events ein und verändert den Worker nicht.
Die Administration besitzt im API-Host ein getrenntes lokales Cookie-Scheme mit Policies,
CSRF, Audit und Operations; eine allgemeine Community-Authentication bleibt außerhalb des
Scopes. Administration UI V1.1 ergänzt dafür eine serverseitig gerenderte Razor-Shell mit
responsiver Mobile-Navigation, Skip-Link, ARIA-/Keyboard-/Focus-Baseline, Reduced-Motion-
Unterstützung sowie nativer DE-/EN-Lokalisierung. Deutsch ist Default und Fallback; die
individuelle Administrator-Sprache wird unter `/admin/account` persistiert und beim Login
erneut angewendet. Der API-Host produziert
`shop.purchase-completed` v1 in die Outbox; der separate Worker kennt das Event über
`Shop.Contracts` und verarbeitet es mit dem Notifications-Consumer
`notifications.shop-purchase`. Shop-Implementierung und Shop-Migrationen werden dort nicht
geladen. Warenkorb, Stock, Discounts und Refunds bleiben ebenfalls außerhalb dieses Slices.
Details stehen in
[shop.md](shop.md).

Streamer.bot wird später als externer Adapter behandelt und lädt keine internen FlurNetz-Assemblies. Interne FlurNetz-Projekte verwenden .NET 10. PostgreSQL ist die primäre relationale Datenbank; die technische Grundlage dafür liegt in `FlurNetz.Persistence` mit Npgsql und Dapper.

Die technische Messaging Foundation ist jetzt in `FlurNetz.Messaging` implementiert. Sie trennt interne Domain Events von Integration Events, bietet einen In-Process-Dispatcher sowie eine PostgreSQL-Outbox und Inbox mit Retry, Poison-Status und Deduplizierung. Der API-Host besitzt für den Shop-Purchase eine producer-only Registry-/Serializer-/Outbox-Runtime und verarbeitet die Outbox nicht selbst. Der Worker betreibt den Engagement→Progression-Workflow und den Shop→Notifications-Workflow über `OutboxProcessor` und die jeweilige Inbox. Die Foundation bleibt fachlich neutral und referenziert kein Modul; der Worker ist eine separate Composition Root. Details stehen in [messaging.md](messaging.md) und [worker.md](worker.md).

Notifications ist ein Consumer-/Policy-Modul und besitzt die persönliche Tabelle
`community_notifications` mit vollständigen historischen Snapshots, Read-/Unread-Lifecycle,
Unread Count und identity-isolierter Keyset-Pagination. `Notifications.Contracts` veröffentlicht
nur die caller-neutrale `ICommunityNotificationCreate`; die HTTP-Grenze verwendet API-eigene DTOs.
Notification-Insert und Messaging-Inbox-Eintrag
werden beim Shop-Consumer in derselben Processor-Transaktion persistiert. Bereits früher
verarbeitete Outbox-Nachrichten werden nicht historisch backgefüllt. Details stehen in
[notifications.md](notifications.md).

`FlurNetz.BuildingBlocks` ist bewusst minimal gehalten und enthält ausschließlich domain-neutrale Primitives. Es gibt dort keine fachlichen Modelle, Generic Repositories oder fachlichen Services. Die Architekturtests sichern die heute prüfbaren Projekt- und Namespace-Grenzen automatisiert ab.

Die Regeln für die Aufnahme weiterer gemeinsamer Bausteine sind in [building-blocks.md](building-blocks.md) festgehalten.

Die Persistence Foundation stellt einen SQL-first Migration Runner und eine technische Migration-History bereit. Spätere Fachmodule liefern ihre Migrationen selbst und bleiben Eigentümer ihrer fachlichen Tabellen. Identity, Engagement, Progression, Economy, Rewards, Inventory, Titles, Shop, Notifications und Integrations besitzen jeweils eigene fachliche Migrationen. Progression, Economy, Rewards, Inventory, Titles, Shop und Notifications verwenden für ihre atomaren Mutationen PostgreSQL-Transaktionen und gezielte Zeilensperren; Integrations verwendet für den Mapping-Link eine explizite PostgreSQL-Transaktion und den moduleigenen Primary Key. Die technischen Grenzen und Konventionen sind in [persistence.md](persistence.md) beschrieben.

`FlurNetz.Messaging` darf auf BuildingBlocks und Persistence zeigen, nicht umgekehrt. Die Outbox verwendet die vorhandene Persistence-Transaktionskapselung; die unabhängigen Hosts rufen den Processor ausdrücklich auf. Es gibt keinen externen Message Broker.

Die Fachmodule verwenden jeweils das Muster `FlurNetz.Modules.<Module>.Contracts` und `FlurNetz.Modules.<Module>`. Die Implementierung darf nur ihr eigenes Contracts-Projekt und ausdrücklich erlaubte technische Infrastruktur sowie öffentliche Cross-Module-Contracts referenzieren; Engagement verwendet zusätzlich `Identity.Contracts`, Persistence und Messaging, um Activity und Outbox atomar zu speichern. Progression verwendet zusätzlich `Identity.Contracts`, Persistence, Messaging und ausschließlich `Engagement.Contracts`, um das Event als `1 XP` zu interpretieren. Economy verwendet `Identity.Contracts`, Persistence und den eigenen Economy-Contract; Rewards verwendet zusätzlich `Identity.Contracts`, `Economy.Contracts` und Persistence, aber niemals die Economy-Implementierung. Inventory verwendet den eigenen Contract, `Identity.Contracts` und Persistence; Shop kennt es
nur über `IInventoryQuantityGrant`. Shop verwendet `Shop.Contracts`,
`Identity.Contracts`, `Economy.Contracts`, `Inventory.Contracts`, BuildingBlocks,
Messaging und Persistence; fremde Modulimplementierungen, Administration, API und Worker bleiben
ausgeschlossen. Titles verwendet den eigenen Contract, `Identity.Contracts` und Persistence; Messaging, Rewards, Achievements, Shop, API und Worker bleiben ausgeschlossen. Notifications verwendet zusätzlich `Identity.Contracts`, `Shop.Contracts`, Messaging und Persistence, aber keine fremde Modulimplementierung; `Notifications.Contracts` veröffentlicht ausschließlich die caller-neutrale Create-Capability. Fremde Modulimplementierungen sind ausgeschlossen. Identity bleibt das erste Referenzmodul; Engagement veröffentlicht die erste fachliche Integration-Nachricht, Progression ist der erste Consumer und Notifications konsumiert `shop.purchase-completed` v1. Die E2E-Workflows sind implementiert, getestet und werden durch den unabhängigen Worker-Host dauerhaft betrieben. Historischer Shop-Backfill ist nicht Bestandteil von Notifications V1. Die vollständige Modul-Liste und Umsetzungsreihenfolge stehen in [modules.md](modules.md).
