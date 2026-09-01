# Änderungsprotokoll

## [Unveröffentlicht]

### Hinzugefügt

- Getrennte HTTP-Management-Grenze für den vollständigen Shop-Angebotskatalog unter
  `/api/admin/shop/offers` mit Create, Get, List, gezielten Feldmutationen sowie Enable und
  Disable ergänzt.
- Die Management-Grenze verwendet die vorhandenen Shop-Application-Use-Cases und deren
  `SELECT FOR UPDATE`-Transaktionsgrenze. Sie besitzt eigene API-Request-/Response-Verträge,
  lässt die Storefront-Semantik unverändert und bildet bekannte Eingabe- und NotFound-Fehler
  als ProblemDetails ab.
- Echte PostgreSQL-/WebApplicationFactory-Tests für serverseitige Offer-ID, Create-Felder,
  vollständige interne Katalogsicht, alle Mutationen, No-ops, Fehlerfälle sowie die
  Auswirkungen auf Storefront, Purchase-Preis, Availability und Kauflimit ergänzt.
- Dokumentiert, dass für diesen HTTP-Adapter keine neue Migration, kein neues Event, kein
  Shop-Consumer und keine Worker-Änderung erforderlich ist. Authentication/Authorization der
  Management-Routen bleibt ein bewusst separater späterer Security-/Host-Scope.

- HTTP-Purchase-Endpunkt `POST /api/shop/offers/{offerId}/purchases` im bestehenden
  `ShopEndpoints`-Vertical-Slice ergänzt. Der API-eigene Request enthält nur `requestId` und
  `communityIdentityId`; bei Erfolg wird der vollständige bestehende `ShopPurchaseResponse`
  mit `201 Created` und Location `/api/shop/purchases/{purchaseId}` geliefert.
- Den vorhandenen `PurchaseShopOffer`-Flow als einzige Purchase-Fachlogik über HTTP erreichbar
  gemacht. `ShopPurchaseRequestId` bleibt die globale Idempotenzgrenze; identische Requests
  liefern dieselbe Purchase-ID und erzeugen keine zweiten Economy-, Inventory-, Purchase- oder
  Outbox-Effekte.
- Gezielte HTTP-ProblemDetails-Abbildung für ungültige Identifier (`400`), unbekanntes Offer oder
  unbekannte Identity (`404`) sowie nicht kaufbare Offers, Kauflimit, Idempotenzkonflikt und
  unzureichenden Economy-Saldo (`409`) ergänzt. Unerwartete Fehler bleiben `500`.
- API-Producer-Runtime mit einer ausschließlich für `shop.purchase-completed` v1 registrierten
  Integration-Event-Registry, Serializer, `PostgreSqlOutboxPublisher` und
  `MessagingMigrationSource` verdrahtet. Der API-Host registriert weder OutboxProcessor,
  Messaging-Worker noch Consumer.
- `AddEconomyDebitCapability()` und `AddInventoryGrantCapability()` als schmale Runtime-
  Registrierungen ergänzt; `AddEconomyModule()` und `AddInventoryModule()` behalten ihre
  vollständige Komposition ohne doppelte Services.
- API-Startup um die bestehenden Migrationen `Economy:1:CreateCommunityEconomies`,
  `Inventory:1:CreateCommunityInventoryEntries` und `Messaging:1:CreateOutboxAndInbox`
  erweitert. Es wurde keine neue Migration angelegt und keine bestehende Migration verändert.
- Echte API-PostgreSQL-Integrationstests für bezahlte und kostenlose Purchases, Replay,
  Idempotenzkonflikt, Fehler-Rollback, Kauflimit, Eingabevalidierung, Producer-only-Outbox und
  vollständige API-Startup-Migrationen ergänzt. Economy-, Inventory-, Messaging- und
  Migration-State wird zwischen API-Tests zurückgesetzt.

- Shop-Runtime-Wiring im separaten Worker ergänzt: `shop.purchase-completed` v1 wird über
  `FlurNetz.Modules.Shop.Contracts` explizit registriert, ohne Referenz auf die Shop-
  Implementierung oder die Shop-Migrationen.
- Worker-Startup-Validierung für die Engagement- und Shop-Event-Zuordnung erweitert. Die
  vorhandene Progression-Consumer-Registration und die Auflösbarkeit des `OutboxProcessor`
  bleiben geprüft; ein fachlicher Shop-Consumer wird weiterhin bewusst nicht registriert.
- Echte PostgreSQL-Worker-Integration für ein nach dem Startup eingereihtes
  `shop.purchase-completed` ergänzt: Der Event wird durch den kontinuierlichen Worker als
  `processed` abgeschlossen, erzeugt ohne Consumer keinen Inbox-Eintrag und bleibt weder
  Retry- noch Failed-Nachricht. Der Migrationstest stellt zusätzlich das Ausbleiben aller
  Shop-Fachtabellen sicher.
- Gezielten Messaging-Integrationstest für bekannte Eventtypen ohne registrierten Consumer
  ergänzt. Der Fall bleibt erfolgreich, ohne Inbox-Write und ohne Retry-/Poison-Behandlung;
  die Outbox-Semantik ist kein späteres Replay-Log.

- Read-only Shop-HTTP-API im echten `FlurNetz.Api` ergänzt:
  `GET /api/shop/offers`, `GET /api/shop/offers/{offerId}`,
  `GET /api/shop/purchases/{purchaseId}` und die identity-isolierte Purchase-History.
  Die Storefront liefert ausschließlich enabled und aktuell verfügbare Angebote.
- API-eigene DTOs sowie ein versionierter, opaker UTF-8-JSON-/Base64Url-Keyset-Cursor für die
  Purchase-History ergänzt; Slice 7 erweitert diese HTTP-Grenze um den Purchase-POST.
- Die interne `AddShopReadOnlyModule()`-Basis bleibt für Storefront-Hosts erhalten; der API-Host
  verwendet für den vollständigen Purchase-Slice nun `AddShopModule()`. In dieser damaligen
  Slice-Stufe bestanden noch keine HTTP-Routen für Katalogmutationen.
- API-Startup führt neben Identity und den beiden Shop-Migrationen nun auch die bestehenden
  Economy-, Inventory- und Messaging-Migrationen aus. Keine neue Migration wurde eingeführt;
  alle bestehenden Migrationen und ihre SQL-Checksums bleiben unverändert.
- Unit-, Architektur- und echte PostgreSQL-API-Integrationstests für Storefront-Filterung,
  DTO-Abbildung, Purchase-Lookup, History-Isolation, Keyset-Roundtrip, Cursor-Validierung und
  API-Komposition ergänzt.
- Read-only Purchase-History-Queries mit `GetShopPurchase` und
  `ListShopPurchasesForIdentity` ergänzt. Einzelne Käufe werden über `ShopPurchaseId`
  geladen; die identitätsgebundene Historie verwendet newest-first Keyset-Pagination über
  `purchased_at DESC, id DESC` mit Page Size 1–100 und Default 50.
- Implementation-eigenen `ShopPurchaseHistoryCursor`, `ShopPurchaseHistoryPage` und
  gezielten `ShopPurchaseHistoryStore` mit Dapper ergänzt. Der Store liest ausschließlich
  `shop_purchases`, verwendet den vorhandenen Identity-/Zeit-Index, rehydriert vollständige
  Purchase-Snapshots und eröffnet für die beiden Reads weder zusätzliche Transaktionen noch
  Locks. Eine weitere Shop-Migration oder Contracts-Erweiterung wurde nicht eingeführt.
- Unit-, Architektur- und echte PostgreSQL-Integrationstests für Cursor-Validierung,
  Identity-Bindung, Page Size, Rehydration, Isolation, gleiche Zeitstempel und
  mehrseitige Keyset-Pagination ergänzt. Ein Cross-Page-Snapshot sowie API, Admin UI,
  Worker-Consumer, variable Purchase-Menge, Cart, Stock, Discounts, Coupons und Refunds
  bleiben ausgeschlossen.
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
- Administration, HTTP-Routen für Katalogmutationen und ein fachlicher Shop-Event-Consumer
  blieben in dieser damaligen Foundation-Stufe bewusst ausgeschlossen; der API-Producer und das
  separate Worker-Wiring waren vorhanden.
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
