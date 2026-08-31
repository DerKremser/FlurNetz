# Änderungsprotokoll

## [Unveröffentlicht]

### Hinzugefügt

- Ersten atomaren Inventory-Shop-Kauf mit `PurchaseShopOffer`, serverseitiger
  `ShopPurchaseId` und global eindeutiger `ShopPurchaseRequestId` für Idempotenz ergänzt.
- `Shop:2:CreateShopPurchases` mit `shop_purchases`, `shop_purchase_requests` und
  `shop_purchase_guards` ergänzt; der einzige neue Foreign Key bleibt Shop-intern von
  Purchase auf Offer.
- Caller-neutrale transaction-aware Capabilities
  `ICommunityIdentityExistence`, `IEconomyBalanceDebit` und
  `IInventoryQuantityGrant` ergänzt, ohne fremde Modulimplementierungen oder Tabellen-SQL
  in Shop einzuführen.
- Atomare PostgreSQL-Orchestrierung für Idempotenz-Reservation, Identity-Prüfung,
  Offer-Snapshot, Kauflimit, Economy-Debit, Inventory-Grant, Purchase-Persistenz und Outbox
  innerhalb einer gemeinsamen Transaktion ergänzt.
- Producer-owned `ShopPurchaseCompletedIntegrationEvent` mit stabilem Message Type
  `shop.purchase-completed` und Schema-Version `1` ergänzt.
- Shop-Purchase-Integrationstests gegen echtes PostgreSQL für gemeinsamen Commit,
  parallele Duplicate-Requests, Idempotency-Conflict, konkurrierendes Kauflimit und
  vollständigen Rollback bei unzureichendem Economy-Saldo ergänzt.
- Shop-Textgrenzen auf Unicode-Skalarwerte vereinheitlicht, U+0000 und nicht wohlgeformtes
  UTF-16 abgewiesen und die `varchar(200)`-/`varchar(2000)`-Semantik mit PostgreSQL angeglichen.
- `AvailabilityWindow` auf kanonische UTC-Instants mit expliziter PostgreSQL-kompatibler
  Mikrosekundenpräzision begrenzt; Sub-Mikrosekundenwerte werden kontrolliert abgewiesen.
- Den Shop-Mutations-Callback technisch als synchronen `Func<ShopOffer, bool>` festgelegt.
- Deterministische PostgreSQL-Concurrency-Tests sowie belastbare Migration-Scope-, Rollback-
  und No-op-Datenbanktests ergänzt.
- Persistierten Shop-Angebotskatalog mit `shop_offers` und der Migration `Shop:1:CreateShopOffers` ergänzt.
- Interne Shop-Katalog-Use-Cases für Create, Get, List, Rename, Description-, Preis-, Availability- und Kauflimitänderungen sowie Enable/Disable und den gezielten `ShopOfferStore` ergänzt.
- Kontrollierte `ShopOffer.Rehydrate`-Domainlösung sowie atomare Row-Lock-Mutationen über `SELECT FOR UPDATE` ergänzt.
- Echte Shop-PostgreSQL-Integrationstests für Migration, exaktes Schema, DB-Constraints, Roundtrips und Nebenläufigkeit ergänzt.
- API, Administration, Shop-Event-Consumer und Worker-Wiring bleiben im Purchase-Slice bewusst ausgeschlossen.
- Ersten Shop-Foundation-Slice mit `ShopOffer`, `ShopPrice`, `AvailabilityWindow` und gezielten Domainmutationen für fachliche Shop-Angebote hinzugefügt.
- Stabilen öffentlichen `ShopOfferId`-Contract und die gemeinsame Verwendung von `Inventory.Contracts.ItemDefinitionId` im Shop ergänzt.
- Shop-Unit- und Architekturtests für Angebotsinvarianten, Zeitfenster, Aktivierung, Kauflimits und Modulgrenzen ergänzt.
- `ItemDefinitionId` aus der Inventory-Domain in `FlurNetz.Modules.Inventory.Contracts` verschoben; Bestandsoperationen und Persistence bleiben intern.
- Ersten persistierten Achievements-Vertical-Slice mit implementation-eigenem Definitionskatalog und permanenten Community-Achievements hinzugefügt.
- `AchievementDefinitionId`, `AchievementDefinition` und immutable `CommunityAchievement` mit kanonischer Unicode-Whitespace- und UTC-Semantik ergänzt.
- Interne Achievements-Use-Cases für Create/Get/List, Rename, Description-Änderung sowie idempotenten Community-Unlock/Get/List ergänzt.
- PostgreSQL-/Dapper-Stores mit `SELECT FOR UPDATE` für Katalogmutationen und atomarem `ON CONFLICT DO NOTHING` für Community-Unlocks ergänzt; der erste erfolgreiche Write gewinnt.
- Achievements-eigene Migration `Achievements:1:CreateAchievementDefinitionsAndCommunityAchievements` mit Definitions- und Community-Tabelle, internem Definition-Foreign-Key und ohne Identity-Foreign-Key ergänzt.
- Achievements-Unit-, echte PostgreSQL-Integration- und Architekturtests für Invarianten, Persistenz, Rollback, Idempotenz und Nebenläufigkeit ergänzt.
- Messaging, Rewards, Economy, Inventory, Titles, Shop, Runtime-Trigger, API und Worker bleiben in diesem Slice bewusst ausgeschlossen.
- Minimale Titles-Domain für freigeschaltete und aktuell ausgewählte Community-Titel.
- Stabile `TitleDefinitionId` sowie invariantengesicherte `Unlock`-, `Lock`-, `SetCurrent`- und `ClearCurrent`-Operationen.
- Idempotente Titelberechtigungen ohne Persistence-, Rewards-, Achievement- oder Shop-Kopplung.
- Erster persistierter Titles-Vertical-Slice mit `CommunityTitles.Rehydrate` und atomarer Unlock-, Lock-, SetCurrent- und ClearCurrent-Persistenz.
- Titles-eigene PostgreSQL-Migration mit drei modul-owned Tabellen und einer Datenbankinvariante von Current zu Unlock über interne Foreign Keys.
- PostgreSQL-Root-Row-Locking pro `CommunityIdentityId` zum Schutz vor verlorenen Änderungen bei konkurrierenden Titles-Operationen.
- Echte Titles-PostgreSQL-Integrationstests für Migration, Constraints, Rehydration, Rollback, Isolation und Nebenläufigkeit.
- Ersten persistierten Titles-Definitionskatalog mit `TitleDefinition`, normalisiertem Anzeigenamen und optionaler Beschreibung.
- Interne Katalog-Use-Cases für Create, Rename, Description-Änderung, Get und List mit `TitleDefinitionStore`.
- Neue Migration `Titles:2:CreateTitleDefinitions` mit kanonischen PostgreSQL-Text-Constraints; die bestehende Titles-V1-Migration bleibt unverändert.
- PostgreSQL-Row-Locking mit `SELECT FOR UPDATE` schützt Katalogmutationen derselben Definition vor Lost Updates; echte Katalog- und Concurrency-Integrationstests ergänzen die Titles-Testabdeckung.
- Erster persistierter Inventory-Vertical-Slice mit atomaren Add-/Remove-Operationen.
- Inventory-eigene PostgreSQL-Migration mit Composite Primary Key und Nichtnegativ-Check.
- Sparse Inventory-Persistenz: Bestände bei null werden gelöscht und fehlende Removes legen keine Zeile an.
- Echte PostgreSQL-Integrationstests für Lifecycle, Rollback, Isolation und konkurrierende Bestandsänderungen.
- Minimale Inventory-Domain für community-bezogene Item-Bestände.
- Stabile `ItemDefinitionId` sowie nicht-negative, overflow-sichere Inventory-Mengen.
- Fachliche Bestandsänderungen mit Schutz vor Unterbestand und ohne Rewards-/Shop-Kopplung.
- Erster persistierter Rewards-Vertical-Slice mit Reward Definitions, Packages und eindeutigen Grant-Records.
- Atomare Economy-Balance-Rewards über eine schmale öffentliche Economy-Contract-Grenze.
- Idempotente Grant-Ausführung über `SourceType`, `SourceId` und `RewardDefinitionId` einschließlich Nebenläufigkeits- und Rollback-Tests.
- Minimale Rewards-Domain mit Reward Definitions, Packages, Sources und Grant-Records.
- Erste konkrete Reward Definition für eine spätere Economy-Balance-Gutschrift.
- Minimale Economy-Domain für nicht-negative Community-Salden.
- Fachliche Gutschriften und Abbuchungen mit Schutz vor Überziehung und Overflow.
- Erster persistierter Economy-Vertical-Slice für atomare Community-Salden.
- Nebenläufigkeitssichere Gutschriften und Abbuchungen mit PostgreSQL-Row-Locking.
- Economy-eigene Migration mit Datenbankinvariante für nicht-negative Salden.
- Erster dauerhaft laufender FlurNetz-Worker zur kontinuierlichen Verarbeitung der PostgreSQL-Outbox.
- Explizite Runtime-Komposition des Engagement-Message-Events mit dem Progression-Consumer.
- Erster zuverlässiger Cross-Module-Workflow von Engagement zu Progression über Outbox und Inbox.
- Normalisierte Message-Aktivitäten können über den Progression-Consumer einmalig 1 XP vergeben.
- Atomare Producer- und Consumer-Transaktionen verhindern verlorene beziehungsweise doppelte fachliche Effekte.
- Erster persistierter Progression-Vertical-Slice für atomare Experience-Point-Vergaben.
- Progression-eigene PostgreSQL-Migration und nebenläufigkeitssichere XP-Akkumulation.
- Minimale Progression-Grundlage für nicht-negative Experience Points.
- Community-bezogener Progressionszustand auf Basis der internen `CommunityIdentityId`.
- Erster Engagement-Vertical-Slice zum Aufzeichnen normalisierter Message-Aktivitäten.
- Engagement-eigene PostgreSQL-Migration und Persistenz für Community-Aktivitäten.
- Fachliche Engagement-Grundlage für normalisierte Community-Aktivitäten.
- Engagement-Aktivitäten verwenden die interne `CommunityIdentityId` statt externer Plattformidentitäten.
- Initiales Repository- und Solution-Grundgerüst für FlurNetz.
- Technische PostgreSQL-Persistenzgrundlage mit Npgsql und Dapper.
- SQL-first Migration Runner mit Migration Ownership, Migration-History und unveränderlichen SQL-Checksums.
- Unit-, Architektur- und echte PostgreSQL-Integrationstests für Verbindungen, Transaktionen und Migrationen.
- Domain-neutrale BuildingBlocks-Grundlage mit Result-/Error-Primitives, Guards und Clock-Abstraktion.
- Erste automatisierte Architekturtests zur Absicherung zentraler Projektgrenzen.
- Messaging Foundation mit getrennten Domain- und Integration-Event-Verträgen sowie deterministischem In-Process-Dispatcher.
- PostgreSQL-Outbox und Inbox für atomare, zuverlässige und deduplizierte Integration Events.
- Explizite Message-Type-Registry, versionierte System.Text.Json-Serialisierung, Claiming sowie Retry-/Failed-Fehlerbehandlung.
- Unit-, Architecture- und echte PostgreSQL-Integrationstests für atomare Verarbeitung, transactional Inbox, Duplicate Redelivery, paralleles Claiming und Poison Messages.
- Physische Contracts- und Implementierungsprojekte für alle vorgesehenen Fachmodule.
- Modulbezogene xUnit-v3-Testprojekte und Architekturtests zur Absicherung der Modul- und Assembly-Grenzen.
- Erste fachliche Identity-Grundlage mit stabiler interner Community-Identity-ID.
- Minimales Domain-Modell für die interne Community-Identität.
- Erster Identity-Vertical-Slice zum Erzeugen, Persistieren und Laden einer internen Community-Identität.
- Identity-eigene PostgreSQL-Migration für die minimale `community_identities`-Tabelle sowie echte PostgreSQL-Integrationstests.
- Erster ausführbarer ASP.NET-Core-API-Host als Composition Root.
- HTTP-Endpunkt `POST /api/identities` zur Erzeugung interner Community-Identitäten.
- Echte API-Integrationstests vom HTTP-Request bis zur PostgreSQL-Persistierung.

### Geändert

- `IntegrationEventEnvelope` weist normalisierte `CorrelationId` und `CausationId` jetzt
  korrekt seinen Properties zu; dadurch persistiert die Outbox die technische
  Shop-Purchase-Request-Korrelation tatsächlich.

- Der bestehende Engagement-zu-Progression-Workflow kann nun außerhalb von Tests kontinuierlich durch einen eigenen Worker-Host verarbeitet werden.
