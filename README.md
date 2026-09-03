# FlurNetz

FlurNetz ist ein modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst eine minimale BuildingBlocks-Grundlage, die technische Persistence Foundation, die Messaging Foundation, die physischen Grenzen der vorgesehenen Fachmodule, den ersten fachlichen Identity-Vertical-Slice, den ersten Engagement-Message-Recording-Slice mit Outbox, den ersten Progression-Inbox-Consumer, den ersten persistierten Economy-Vertical-Slice, den ersten persistierten und ausführbaren Rewards-Vertical-Slice, den ersten persistierten Inventory-Vertical-Slice, den ersten persistierten Titles-Vertical-Slice, den ersten persistierten Achievements-Vertical-Slice sowie den vollständigen Shop-V1-Stand mit persistiertem Angebotskatalog,
atomarem Shop-Inventory-Kauf, persistierter Shop-Kaufhistorie und Shop-HTTP-API
mit read-only Storefront, HTTP-Purchase und HTTP-Katalogverwaltung sowie unabhängige API- und Worker-Hosts. Der Cross-Module-Workflow ist
Ende zu Ende gegen PostgreSQL getestet und kann durch den Worker kontinuierlich verarbeitet
werden; die persönliche Notifications-Inbox V1 ist über HTTP lesbar und wird im Worker aus
`shop.purchase-completed` v1 befüllt. Die Administration V1 ergänzt eine geschützte lokale
Cookie-Administration mit Login, Permissions, Audit, idempotenten Operations, Owner-Reads und
Razor-UI für die vereinbarten Adminbereiche. Engagement besitzt weiterhin keine eigene
öffentliche HTTP-Schnittstelle, Automation bewusst keine öffentliche Runtime-API, sondern nur
die interne Management-Grenze.

## Automation V1

Automation ist als persistierte, betreiberkonfigurierbare Rule Engine umgesetzt. Die Engine
verarbeitet ausschließlich engagement.message-recorded v1 und shop.purchase-completed v1
über die expliziten Consumer automation.engagement-message-recorded und
automation.shop-purchase-completed. Rules werden über die interne Management-Grenze
/api/admin/automation/rules verwaltet, starten deaktiviert und besitzen einen terminalen
Archivzustand.

V1 unterstützt fünf strikt validierte AND-Conditions, economy.credit und
notification.create mit stabiler Action-Reihenfolge. Execution-Reservation,
AutomationExecution-History und die Economy-/Notification-Writes teilen die vorhandene
Messaging-Transaktion. Der Worker führt die Rules aus; die API bleibt Management-Host.
Details stehen in docs/architecture/automation.md.

## Overlay V1

Overlay V1 ist eine persistierte OBS-/Browser-Source-Alert-Pipeline. Channels werden über
die interne Management-Grenze `/api/admin/overlay/channels` angelegt und verwaltet; Create
und Rotation geben einen kryptographisch zufälligen Source Key genau einmal aus. PostgreSQL
speichert nur dessen SHA-256-Hash. Die relative Browser Source `/overlay/{sourceKey}` zeigt
Alerts über SSE (`/api/overlay/sources/{sourceKey}/stream`) mit FIFO-Queue, Tail-Cursor,
Reconnect, Heartbeats und begrenzter Browser-Deduplizierung an.

Automation unterstützt zusätzlich `overlay.alert` über ausschließlich
`FlurNetz.Modules.Overlay.Contracts`. Der Alert-Write verwendet die bestehende Messaging-
Transaktion; ein deaktivierter, archivierter oder fehlender Zielchannel wird fachlich
unterdrückt und nicht als Worker-Poison-Fehler behandelt. Der API-Host besitzt HTTP-/SSE-
Verantwortung, der Worker nur die Runtime-Komposition. Details stehen in
[docs/architecture/overlay.md](docs/architecture/overlay.md).

## Technische Basis

- .NET 10 für interne FlurNetz-Projekte
- C# 14
- modulare Architektur mit klarer Trennung der Fachmodule
- `System.Text.Json` als Standard für JSON
- `Microsoft.Extensions.Logging` als Logging-Basis
- PostgreSQL als primäre relationale Datenbank
- Npgsql als PostgreSQL-Treiber und Dapper als schlanke SQL-Datenzugriffsbasis
- xUnit v3 für die technische Testgrundlage

Die Persistence Foundation stellt asynchrone PostgreSQL-Verbindungen und technische Transaktionsgrenzen bereit. Ein SQL-first Migration Runner verwaltet migrationsübergreifend eine technische Migration-History mit stabiler Identität und SQL-Checksum.

## Messaging Foundation

`FlurNetz.Messaging` trennt interne Domain Events von serialisierbaren Integration Events. Domain Events werden sequenziell und deterministisch im Prozess verteilt. Integration Events besitzen einen Envelope mit `MessageId`, logischem Nachrichtentyp, Schema-Version und UTC-Zeitpunkt; eine explizite Registry ordnet Typ und Version sicher einem CLR-Payload-Typ zu und `System.Text.Json` übernimmt die UTF-8-Serialisierung.

Die PostgreSQL-Outbox wird über dieselbe `PostgreSqlTransaction` wie ein fachlicher Datenbank-Write befüllt. Dadurch sind Business Write und Outbox Insert gemeinsam commit- oder rollbackfähig. Ein aufrufbarer Outbox Processor verwendet PostgreSQL-Leases, Inbox-Deduplizierung pro stabiler Consumer Identity, Retry und einen isolierten Failed/Poison-Status. `FlurNetz.Worker` ruft diesen Processor als erster dauerhaft laufender Runtime-Host kontinuierlich auf; es gibt keinen externen Broker.

Der Worker registriert `engagement.message-recorded` v1 und `shop.purchase-completed` v1
explizit über ihre Contracts. Der stabile Notifications-Consumer
`notifications.shop-purchase` schreibt die persönliche Notification zusammen mit seinem
Messaging-Inbox-Eintrag atomar. Die Outbox ist kein Event Store und kein Replay-Log: Bereits
früher ohne diesen Consumer verarbeitete Shop-Events werden nicht historisch backgefüllt.

Details und die technischen Tabellen stehen in [docs/architecture/messaging.md](docs/architecture/messaging.md). Die Tests in `FlurNetz.Messaging.IntegrationTests` verwenden echtes PostgreSQL über Testcontainers; Docker oder alternativ `FLURNETZ_TEST_CONNECTION_STRING` ist dafür erforderlich.

## BuildingBlocks und Architekturtests

`FlurNetz.BuildingBlocks` enthält ausschließlich kleine, domain-neutrale Primitives für eine spätere gemeinsame Nutzung. Dazu gehören Result-/Error-Typen, generische Guards, die minimale `IClock`-Abstraktion und deren neutrale `SystemClock`-Implementierung.

Die Projekte `FlurNetz.BuildingBlocks.Tests`, `FlurNetz.Persistence.Tests`, `FlurNetz.Messaging.Tests`, `FlurNetz.Messaging.IntegrationTests`, `FlurNetz.Modules.Identity.Tests`, `FlurNetz.Modules.Identity.IntegrationTests`, `FlurNetz.Modules.Engagement.Tests`, `FlurNetz.Modules.Engagement.IntegrationTests`, `FlurNetz.Modules.Progression.Tests`, `FlurNetz.Modules.Progression.IntegrationTests`, `FlurNetz.Modules.Economy.Tests`, `FlurNetz.Modules.Economy.IntegrationTests`, `FlurNetz.Modules.Rewards.Tests`, `FlurNetz.Modules.Rewards.IntegrationTests`, `FlurNetz.Modules.Inventory.Tests`, `FlurNetz.Modules.Inventory.IntegrationTests`, `FlurNetz.Modules.Titles.Tests`, `FlurNetz.Modules.Titles.IntegrationTests`, `FlurNetz.Modules.Achievements.Tests`, `FlurNetz.Modules.Achievements.IntegrationTests`, `FlurNetz.Modules.Integrations.Tests`, `FlurNetz.Modules.Integrations.IntegrationTests`, `FlurNetz.Modules.Shop.Tests`, `FlurNetz.Modules.Shop.IntegrationTests`, `FlurNetz.Workflows.IntegrationTests`, `FlurNetz.Api.IntegrationTests` und `FlurNetz.Architecture.Tests` prüfen Primitives, Persistence- und Messaging-Logik, Identity- und Engagement-Vertical-Slices, den Rewards-Katalog und die atomare Rewards-Ausführung, die persistierten Inventory-, Progression-, Economy-, Titles-, Achievements- und Shop-Slices einschließlich Nebenläufigkeit, den Ende-zu-Ende-Workflow gegen PostgreSQL, den HTTP-zu-PostgreSQL-Weg sowie Projekt-, Namespace- und Typgrenzen.

## Identity Foundation und erster Vertical Slice

Identity ist das erste Referenzmodul und besitzt die zentrale interne Identität eines Community-Mitglieds. `FlurNetz.Modules.Identity.Contracts` enthält den stabilen, unveränderlichen Identifier
`CommunityIdentityId` sowie die schmale transaction-aware
`ICommunityIdentityExistence`-Capability; `FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity` mit dieser ID.

Der erste Identity-Use-Case erzeugt eine neue `CommunityIdentityId`, bildet die Domain-Identity und persistiert sie in PostgreSQL. Der Dapper-/Npgsql-Adapter arbeitet gegen die Identity-eigene Tabelle `community_identities`, die ausschließlich `id uuid primary key` enthält; Laden über die interne ID ist ebenfalls enthalten. Externe Plattformkennungen werden über Integrations-Auflösung und Mapping auf die interne FlurNetz-Identität bezogen. Sie ersetzen `CommunityIdentityId` nicht.

Der bestehende `CreateCommunityIdentity`-Use-Case ist über `FlurNetz.Api` als `POST /api/identities` erreichbar. Der HTTP-Adapter akzeptiert keinen Request-Body und gibt bei Erfolg ausschließlich ein API-Response-DTO mit der erzeugten ID zurück. Plattformkonten, Authentifizierung, Profile und fachliche Domain- oder Integration Events sind weiterhin nicht enthalten. Details stehen in [docs/architecture/identity.md](docs/architecture/identity.md) und [docs/architecture/api.md](docs/architecture/api.md).

## Engagement Message Recording

`FlurNetz.Modules.Engagement` enthält den ersten vollständigen Recording-Slice für normalisierte
Message-Aktivitäten. `RecordMessageEngagement` verwendet eine bereits aufgelöste
`CommunityIdentityId`, erzeugt den UTC-Zeitpunkt über `IClock` und persistiert die Aktivität
gemeinsam mit `MessageEngagementRecordedIntegrationEvent` in der Outbox. Der Contract verwendet
den stabilen Message Type `engagement.message-recorded` mit Schema-Version `1` und enthält nur
die interne Identity-Guid. Es werden bewusst weder Nachrichtentext, Plattformdaten noch XP
gespeichert; Engagement ruft Progression nicht direkt auf. Details stehen in
[docs/architecture/engagement.md](docs/architecture/engagement.md).

## Progression Vertical Slice

`FlurNetz.Modules.Progression` enthält den ersten persistierten Vertical Slice für den
fachlichen Fortschritt einer internen `CommunityIdentityId`. `ExperiencePoints` sind
nicht-negativ, immutable und werden ohne stilles `long`-Overflow akkumuliert.
`CommunityProgression` startet mit `0` XP. `GrantExperience` erzeugt den Zustand lazy bei
der ersten Vergabe und speichert positive XP atomar in PostgreSQL. `Progression.Contracts`
bleibt bewusst leer.

Der Persistence-Adapter verwendet `CommunityIdentityId` als Primärschlüssel, ein
`bigint`-XP-Feld mit Nichtnegativ-Check und transaktionales `SELECT FOR UPDATE` gegen Lost
Updates. Der Consumer `progression.message-engagement-xp` verarbeitet das Engagement-Event
über die Inbox-Transaktion und interpretiert jede normalisierte Message als genau `1 XP`.
Duplicate Delivery vergibt dadurch nicht doppelt; Level, Rewards und zusätzliche API-Endpunkte
sind weiterhin nicht Bestandteil. Der Runtime-Consumer wird durch `FlurNetz.Worker` ausgeführt.
Details stehen in [docs/architecture/progression.md](docs/architecture/progression.md).

## Economy Vertical Slice

`FlurNetz.Modules.Economy` besitzt einen kleinen persistierten Vertical Slice für einen neutralen,
nicht-negativen Economy-Saldo je interner `CommunityIdentityId`. `EconomyBalance` verwendet
`long`, schützt Gutschriften vor Overflow und verhindert bei Abbuchungen eine Überziehung.
`CommunityEconomy` startet bei null; nur positive Beträge können gutgeschrieben oder abgebucht
werden. Der interne Store führt Credits und Debits atomar mit PostgreSQL-Transaktionen und
`SELECT FOR UPDATE` aus; ein Credit erzeugt den Zustand lazy erst bei Erfolg.
`Economy.Contracts` enthält die caller-neutralen transaction-aware Fähigkeiten
`IEconomyBalanceCredit` und `IEconomyBalanceDebit`. Rewards verwendet Credit, der
Shop-Purchase Debit; Economy kennt beide Aufrufer nicht.

Eine konkrete Währungsbezeichnung, Multi-Currency, Transfers, Ledger und Economy-API sind
weiterhin nicht enthalten. Details stehen in
[docs/architecture/economy.md](docs/architecture/economy.md).

## Rewards Vertical Slice

`FlurNetz.Modules.Rewards` enthält den ersten persistierten und ausführbaren Vertical Slice für
Reward Definitions, verpflichtende Reward Packages, fachliche Reward Sources und Grant-Records.
Der erste und einzige ausführbare Typ `EconomyBalanceRewardDefinition` beschreibt eine
Economy-Balance-Gutschrift mit einem neutralen `long Amount`; Rewards besitzt den Economy-
Zustand nicht. `RewardGrant` gehört genau zu einer Reward Definition. Die eindeutige Grenze
`SourceType + SourceId + RewardDefinitionId` verhindert doppelte fachliche Effects auch bei
parallelen Wiederholungen; ein Partial-State wird als Fehler abgelehnt. Package-Grants und
Economy-Writes committen oder rollbacken gemeinsam in einer PostgreSQL-Transaktion.

`FlurNetz.Modules.Rewards.Contracts` bleibt bewusst leer. XP bleiben im Progression-Modul;
Messaging, Events, Inventory-/Title-Rewards und Runtime-Trigger sind nicht Bestandteil dieses
Slices. Die API bindet Rewards ausschließlich für die geschützte Administration ein. Details stehen in
[docs/architecture/rewards.md](docs/architecture/rewards.md).

## Erster persistierter Inventory-Vertical-Slice

`FlurNetz.Modules.Inventory` besitzt jetzt neben seiner Domain-Foundation den ersten
persistierten Slice für mengenbasierte Community-Bestände. `CommunityInventoryEntry.Rehydrate`
rekonstruiert gespeicherte Positionen; `ICommunityInventoryStore` bildet die interne atomare
Persistenzgrenze und `AddInventoryQuantity` sowie `RemoveInventoryQuantity` bleiben frei von
SQL- und Transaktionslogik.

Der PostgreSQL-Adapter verwendet den Composite Key
`CommunityIdentityId + ItemDefinitionId` und `SELECT FOR UPDATE` gegen Lost Updates. Add legt
eine fehlende Position lazy an. Die Persistenz bleibt sparse: Erreicht Remove exakt Menge null,
wird die Zeile gelöscht; Remove auf einer fehlenden Position erzeugt keine Nullzeile.
`Inventory:1:CreateCommunityInventoryEntries` erzwingt zusätzlich `quantity >= 0` und besitzt
keinen Cross-Module-Foreign-Key.

`FlurNetz.Modules.Inventory.Contracts` enthält den stabilen Fachtyp `ItemDefinitionId` und die
caller-neutrale `IInventoryQuantityGrant`-Capability. Messaging, ein Item-Katalog und Worker-
Anbindung bleiben außerhalb des Inventory-Moduls; die API nutzt die Grant-Capability im
atomaren Shop-Purchase und die owner-owned Read-/Adjust-Capabilities in der Administration. Details stehen in
[docs/architecture/inventory.md](docs/architecture/inventory.md).

## Titles Vertical Slice

`FlurNetz.Modules.Titles` besitzt nun neben der Domain-Grundlage einen persistierten
Community-State und einen implementation-eigenen Definitionskatalog. `TitleDefinition`
persistiert eine stabile `TitleDefinitionId`, einen kanonischen Anzeigenamen und eine
optionale Beschreibung. Die internen Use Cases unterstützen Create, Get, List, Rename und
Description-Änderung; `TitleDefinitionStore` schützt Mutationen derselben Definition mit
PostgreSQL-Row-Locking und `SELECT FOR UPDATE` vor Lost Updates.

Der Community-Slice verwendet weiterhin `CommunityTitles.Rehydrate` sowie die atomaren
Use Cases `Unlock`, `Lock`, `SetCurrent` und `ClearCurrent`. Eine Root-Zeilensperre
serialisiert Operationen pro `CommunityIdentityId`; ein interner Foreign Key schützt die
Current→Unlock-Invariante. `Titles.Contracts` bleibt bewusst leer. Es gibt keine fachliche
Runtime-API; die geschützte Administration liest und verwaltet den Owner-Zustand über die
bestehenden Use-Cases. Reward-, Achievement- oder Shop-Integration sowie Messaging und Worker
sind nicht an Titles angebunden. Die echten Tests liegen in
`FlurNetz.Modules.Titles.IntegrationTests`. Details stehen in
[docs/architecture/titles.md](docs/architecture/titles.md).

## Achievements Vertical Slice

`FlurNetz.Modules.Achievements` besitzt den ersten persistierten Slice für einen
implementation-eigenen Definitionskatalog und permanente Community-Achievements.
`AchievementDefinition` speichert eine stabile `AchievementDefinitionId`, einen
kanonischen Anzeigenamen und eine optionale Beschreibung. Die internen Use Cases unterstützen
Create, Get, List, Rename, Description-Änderung, Unlock sowie Get und List der Community-
Achievements.

Der Unlock prüft die Definition im eigenen Katalog, bezieht den UTC-Zeitpunkt über `IClock`
und schreibt atomar und idempotent über die Composite-Primary-Key-Tabelle. Der erste
erfolgreiche Write gewinnt; ein Duplicate überschreibt den ursprünglichen Zeitpunkt nicht.
`Achievements.Contracts` bleibt leer. Es gibt keine Runtime-Trigger, Events, Messaging,
Rewards-, Economy-, Inventory-, Titles-, Shop- oder Worker-Anbindung; die geschützte
Administration bindet nur die owner-owned Definition- und Community-Use-Cases ein. Details stehen in
[docs/architecture/achievements.md](docs/architecture/achievements.md).

## Vollständiger Shop-V1-Stand

`FlurNetz.Modules.Shop` enthält den fachlichen Angebotskatalog, den atomaren Inventory-Kauf und
die read-only Kaufhistorie. `Shop.Contracts` veröffentlicht
`ShopOfferId`, `ShopPurchaseId`, `ShopPurchaseRequestId` und
`ShopPurchaseCompletedIntegrationEvent` mit dem stabilen Message Type
`shop.purchase-completed` und Schema-Version `1`. `ShopOffer` verwendet die gemeinsame `ItemDefinitionId` aus
`Inventory.Contracts`, einen `ShopPrice`, einen kanonischen Anzeigenamen, eine optionale
Beschreibung, ein halboffenes `AvailabilityWindow`, ein optionales positives Kauflimit pro
Identität, einen nicht-negativen `SortOrder`, einen Aktivierungszustand und einen endgültigen
Archivierungszustand. Neue Angebote starten deaktiviert, nicht archiviert und standardmäßig mit
`SortOrder = 0`; Ziel-IDs bleiben
unveränderlich, Änderungen erfolgen über gezielte Domainmethoden. `ShopOffer.Rehydrate` stellt
persistierte Angebote mit denselben Domaininvarianten wieder her. Textgrenzen werden nach
Unicode-Skalarwerten passend zur PostgreSQL-Zeichensemantik bewertet; U+0000 und nicht
wohlgeformtes UTF-16 werden abgewiesen. Gesetzte Availability-Grenzen sind kanonische UTC-
Instants mit exakt PostgreSQL-kompatibler Mikrosekundenpräzision.

`Shop:1:CreateShopOffers` besitzt weiterhin ausschließlich `shop_offers`; der Katalog-Store
verwendet gezieltes PostgreSQL-/Dapper-SQL und `SELECT FOR UPDATE` für Mutationen.
`Shop:2:CreateShopPurchases` ergänzt `shop_purchase_requests`,
`shop_purchase_guards` und `shop_purchases`; der einzige Foreign Key ist Shop-intern von
Purchase auf Offer. `Shop:3:AddShopOfferSortOrder` ergänzt ausschließlich die bestehende
Tabelle `shop_offers` um `sort_order integer NOT NULL` mit `sort_order >= 0`.
`Shop:4:AddShopOfferArchiveState` ergänzt `is_archived boolean NOT NULL`, backfillt bestehende Angebote
mit `false`, entfernt den temporären Default wieder und erzwingt den Ausschluss von archiviert
und aktiviert. V1 bis V3 bleiben unverändert.

`ShopOffer` erlaubt gleiche SortOrder-Werte und nimmt keine automatische Umnummerierung vor.
Die verbindliche Katalogreihenfolge ist `sort_order ASC, id ASC`. Der Store liefert diese
Reihenfolge autoritativ; die öffentliche Storefront übernimmt sie nach ihrem Enabled-/Availability-
Filter unverändert.

`PurchaseShopOffer` erzeugt die Purchase-ID serverseitig. Der
`PostgreSqlShopPurchaseExecutor` koordiniert innerhalb einer gemeinsamen
`PostgreSqlTransaction` Idempotenz, Identity-Existenzprüfung, stabilen Offer-Snapshot,
Kauflimit, transaction-aware Economy-Debit, Inventory-Grant um exakt eins, Purchase-Write und
Outbox. Identische Requests erzeugen exakt einen Effekt; Fehler rollen alle Teilwirkungen
gemeinsam zurück. Shop referenziert dabei nur fremde Contracts und keine fremden
Implementierungen oder Tabellen.

`GetShopPurchase` lädt einen Purchase über `ShopPurchaseId`; eine unbekannte ID liefert
`null`. `ListShopPurchasesForIdentity` liest ausschließlich für eine
`CommunityIdentityId` in der Reihenfolge `purchased_at DESC, id DESC`. Die History verwendet
stabile Keyset-Pagination ohne Offset und ohne Gesamtzählung, mit Page Size `1`–`100`
(Default `50`) und einem implementation-eigenen, an die Identity gebundenen Cursor über
Kaufzeitpunkt und Purchase-ID. Der Store liest dafür pro Seite höchstens `pageSize + 1`
Datensätze und rehydriert den vollständigen Snapshot aus `shop_purchases`. Die Read-Queries
eröffnen keine zusätzliche Transaktion und keine Locks; es gibt keinen Cross-Page-Snapshot.
Unbekannte oder historisch leere Identities liefern eine leere Seite ohne Cursor.

Die API stellt weiterhin die read-only Storefront und Purchase-History bereit und ergänzt
`POST /api/shop/offers/{offerId}/purchases`. Der API-eigene Request enthält ausschließlich
`requestId` und `communityIdentityId`; als Erfolg liefert der Adapter den vollständigen
`ShopPurchaseResponse` mit `201 Created` und der Location
`/api/shop/purchases/{purchaseId}`. Die `ShopPurchaseRequestId` liefert die globale
Idempotenzgrenze: Ein identischer Request gibt dieselbe Purchase-ID zurück und belastet Economy,
Inventory, Purchase- und Outbox-Persistenz nur einmal.

Der POST-Adapter validiert nur Route und Request, erzeugt daraus die vorhandenen fachlichen IDs
und ruft `PurchaseShopOffer` auf. Der atomare bestehende Shop-Flow bleibt die einzige Stelle für
Identity-Prüfung, Offer-Snapshot, Kauflimit, Economy-Debit, Inventory-Grant, Purchase und Outbox.
Bekannte Client-/Fachfehler werden als `400`, `404` oder `409` ProblemDetails abgebildet;
unerwartete Fehler bleiben `500`. Der API-Host ist für den Shop ein reiner Event-Producer und
bindet zusätzlich die persönliche Notifications-Inbox ein: Er registriert
`shop.purchase-completed` v1, schreibt die Nachricht als Teil des Purchases in die Outbox und
führt keinen Processor oder Consumer aus. Der separate Worker verarbeitet den Eventtyp über
`Shop.Contracts` und den stabilen Notifications-Consumer `notifications.shop-purchase`.
Bereits verarbeitete Outbox-Nachrichten werden nicht historisch backgefüllt.

Die interne Katalogverwaltung ist zusätzlich als klar getrennte HTTP-Management-Grenze unter
`/api/admin/shop/offers` verfügbar. Sie verwendet ausschließlich die vorhandenen
`CreateShopOffer`, `GetShopOffer`, `ListShopOffers`, `RenameShopOffer`,
`ChangeShopOfferDescription`, `ChangeShopOfferPrice`, `ChangeShopOfferAvailability`,
`ChangeShopOfferPurchaseLimit`, `ChangeShopOfferSortOrder`, `EnableShopOffer` und
`DisableShopOffer`- und `ArchiveShopOffer`-Use-Cases. Der Management-Create kann `SortOrder`
optional setzen; fehlt der
Wert, wird `0` verwendet. Die Sortierung kann später über
`PUT /api/admin/shop/offers/{offerId}/sort-order` geändert werden. Ein negativer Wert ist
ungültig; gleiche Werte sind ein No-op und liefern ebenfalls `204 No Content`. Die Management-
Responses enthalten den aktuellen SortOrder und `IsArchived`. Archivieren ist über
`POST /api/admin/shop/offers/{offerId}/archive` dauerhaft möglich; die Route liefert bei
erstmaliger und wiederholter Archivierung `204 No Content`. Ein archiviertes Angebot bleibt in
der Management-Sicht sichtbar, aber aus der öffentlichen Storefront und dem Kauf ausgeschlossen.
Die Management-Sicht enthält auch deaktivierte, zukünftige und abgelaufene Angebote; die
öffentliche Storefront bleibt auf `IsEnabled && !IsArchived && IsAvailableAt(now)` beschränkt.
Die API führt dafür keine eigene Transaktion ein und erzeugt aus der Management-Grenze keine
Events oder Consumer. Es gibt keine neuen Shop.Contracts und keine neue Event-Version; der
separate Notifications-Consumer verarbeitet das bestehende Event im Worker. Die bestehende
Management-Grenze ist in Administration V1 geschützt, auditierbar und CSRF-gesichert.

Drag & Drop, Bulk-Reorder, Unarchive, Restore, Soft Delete und Hard Delete sind bewusst nicht
Teil des Shop-V1-Scope; die gemeinsame Administration bietet die vereinbarte Katalogansicht
und geschützte Shop-Management-Grenze.

Echte PostgreSQL-Integrationstests prüfen zusätzlich erfolgreichen gemeinsamen Commit,
Duplicate-Request-Idempotenz, Idempotency-Conflict, konkurrierendes Kauflimit und vollständigen
Rollback bei unzureichendem Saldo sowie Lookup, Identity-Isolation, newest-first-Reihenfolge
und mehrseitige History-Pagination ohne Duplikate oder ausgelassene Käufe. Die API-Integration
prüft zusätzlich Storefront-Filterung, DTO-Abbildung, Cursor-Roundtrip und Fehlerfälle.
Warenkorb, variable Purchase-Menge, Stock, Kategorien, zusätzliche Metadaten, Discounts,
Coupons, Refunds und Purchase-Cancellation gehören bewusst nicht zum
Shop-V1-Scope. Die V1-Entscheidungen und der Abschlussaudit stehen in
[docs/architecture/shop.md](docs/architecture/shop.md).

## Notifications V1

Notifications besitzt eine persistente persönliche In-App-Inbox mit Snapshot-Daten, stabiler
Keyset-Pagination, Unread Count sowie Read-/Unread- und Mark-All-Read-Lifecycle. Die HTTP-Grenze
liegt unter `/api/identities/{communityIdentityId}/notifications`. Der Worker konsumiert
`shop.purchase-completed` v1 mit der stabilen Consumer Identity `notifications.shop-purchase`;
Notification und Messaging-Inbox werden gemeinsam transaktional persistiert. Der API-Host
führt keinen Consumer und bietet bewusst keinen öffentlichen Notification-Create-Endpunkt.
Details stehen in [docs/architecture/notifications.md](docs/architecture/notifications.md).

## Persistence Foundation

`FlurNetz.Persistence` verwendet PostgreSQL, Npgsql und Dapper ohne ORM und ohne Generic Repository. Migrationen werden als explizite SQL-Texte von ihren jeweiligen Besitzern bereitgestellt, deterministisch ausgeführt und in `flurnetz_persistence.migration_history` nachverfolgt. Bereits angewendete Migrationen sind unveränderlich; eine abweichende SQL-Checksum führt zu einem Fehler.

`FlurNetz.Persistence.IntegrationTests` testet Verbindungen, Commit/Rollback und den Migration Runner gegen PostgreSQL. Für den automatischen Testlauf wird Docker für Testcontainers benötigt. Alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` auf eine isolierte PostgreSQL-Testdatenbank zeigen.

Identity, Engagement, Progression, Economy, Rewards, Inventory, Titles, Achievements, Shop,
Notifications und Administration besitzen jeweils eigene fachliche beziehungsweise
administrative Tabellen und gezielte Adapter; die fachlichen
Migrationen laufen über dieselbe technische Persistence Foundation. Engagement persistiert Activity
und Outbox atomar. Progression, Economy, Rewards, Inventory, Titles und Notifications verwenden
für konkurrierende beziehungsweise verpflichtende Mutationen atomare Transaktionen und gezielte
Zeilensperren; Achievements verwendet einen atomaren Composite-Key-Insert für idempotente Unlocks.
Rewards und Economy koordinieren ihre Writes über eine gemeinsame Connection/Transaction.
Der Shop-Purchase koordiniert Request-, Guard- und Purchase-Writes mit Identity-Existenzprüfung,
Economy-Debit, Inventory-Grant und Outbox in einer zweiten konkreten gemeinsamen
PostgreSQL-Transaktion. Die read-only Kaufhistorie nutzt dagegen gezielte Einzel-Reads gegen
`shop_purchases` ohne zusätzliche Transaktion, Locks, Identity-Existenzprüfung oder neue
Migration. Beide Kompositionen erzeugen keine Cross-Module-Foreign-Keys. Inventory, Titles und Achievements bleiben ebenfalls frei von Cross-Module-Foreign-Keys auf Identity; Achievements besitzt nur einen internen Foreign Key von Community-Achievements auf seine Definition. Der Titles-Katalog liegt in `title_definitions` und besitzt keinen Unlock→Definition-Foreign-Key. Administration besitzt zusätzlich ausschließlich seine eigenen Tabellen ohne Foreign Keys auf andere Module. API und Worker stellen ihre jeweilige Connection-Konfiguration als unabhängige Composition Roots bereit und führen ihre benötigten Migrationen vor dem Start ihrer Runtime aus; der API-Host bindet für den Shop-Purchase die Economy-/Inventory-Capabilities und für Administration die expliziten Owner-Reads und -Mutationen ein. Der Worker bindet für Automation zusätzlich Economy-Credit und Notification-Create-Capabilities ein. Der Worker verarbeitet die Outbox kontinuierlich über den bestehenden Processor; Engagement, Progression, Economy und Automation sind weiterhin nicht als öffentliche Fach-HTTP-Endpunkte registriert. Live-Plattformverbindungen sind weiterhin nicht implementiert; Integrations V1 bietet die persistierte External-Identity-Mapping-Grenze. Details stehen in [docs/architecture/persistence.md](docs/architecture/persistence.md) und [docs/architecture/administration.md](docs/architecture/administration.md).

## Fachmodule

Für jedes vorgesehene Fachmodul existieren eine Contracts-Class-Library, eine Implementierungs-Class-Library und ein xUnit-v3-Testprojekt. Die noch nicht begonnenen Module bleiben bewusst leer; Identity bildet mit `CommunityIdentityId`, `CommunityIdentity`, Use Case, gezieltem Persistence-Adapter und Migration den ersten fachlichen Vertical Slice. Engagement ergänzt den Message-Recording-Slice mit eigenem Integration Event und atomarem Activity-/Outbox-Write. Progression ergänzt den persistierten XP-Slice mit atomarem Store, Inbox-Consumer und Parallelitätstests. Economy ergänzt den persistierten Saldo-Slice mit atomarem Store, eigener Migration und Parallelitätstests; der API-Host nutzt daraus ausschließlich die schmale Debit-Capability im Shop-Purchase und bietet keinen Economy-Endpunkt. Rewards besitzt nun einen persistierten und ausführbaren Domain-/Application-/Persistence-Slice für Economy-Balance-Gutschriften mit eigener Migration, Idempotenz- und Atomicity-Tests, bleibt aber ohne Runtime-Trigger; die geschützte Administration bietet die explizite Katalog-/Grant-Grenze. Inventory ergänzt den ersten persistierten Vertical Slice mit atomarem PostgreSQL-Store,
eigener Migration und Sparse-Zero-Lifecycle und veröffentlicht jetzt zusätzlich die schmale
caller-neutrale `IInventoryQuantityGrant`-Capability für gemeinsame Transaktionen. Titles ergänzt nun zusätzlich zu Rehydration und Community-State einen persistierten Definitionskatalog mit `TitleDefinition`, internen Create/Get/List/Rename/Description-Use-Cases, `Titles:2:CreateTitleDefinitions`, Row-Locking und echten Katalog-Concurrency-Tests. Achievements ergänzt einen persistierten Definitionskatalog und permanente, atomare, idempotente Community-Unlocks mit eigener Migration und Concurrency-Tests. Shop besitzt mit `Shop:1:CreateShopOffers` den persistierten Angebotskatalog und mit
`Shop:2:CreateShopPurchases` den atomaren Inventory-Purchase inklusive Idempotenz,
Kauflimit, Economy-Debit, Inventory-Grant, Purchase-Persistenz, Outbox und gezielten
read-only History-Queries mit Keyset-Pagination. Der Shop ist über die API für Storefront-
Angebote, Purchase-History und `POST /api/shop/offers/{offerId}/purchases` erreichbar. Der
Purchase-Request enthält nur `requestId` und `communityIdentityId`; der bestehende
`ShopPurchaseResponse` wird mit `201 Created` und Purchase-Location geliefert. Die
`ShopPurchaseRequestId` bildet die globale Idempotenzgrenze. Der API-Host ist Producer für
`shop.purchase-completed` v1 und verarbeitet die Outbox nicht selbst. Der Worker kennt das
Shop-Event über `Shop.Contracts` und registriert den Notifications-Consumer
`notifications.shop-purchase`. Die
HTTP-Katalogverwaltung für den Shop ist über die separate
`/api/admin/shop/offers`-Management-Grenze bereit. Diese ist durch Administration V1
permission- und CSRF-geschützt und wird zusätzlich über Audit/Operations abgesichert. Der
Ende-zu-Ende-Workflow läuft über Outbox, Worker, Inbox und Progression-Consumer. Der Worker ist
kein Fachmodul. Die Grenzen und die spätere Reihenfolge sind in [docs/architecture/modules.md](docs/architecture/modules.md) beschrieben.

Der verbindliche Shop-V1-Katalog verwendet eine nichtnegative, betreibersteuerbare `SortOrder`
mit stabiler Reihenfolge `sort_order ASC, id ASC`; gleiche Werte werden nicht automatisch
umgeordnet. `ArchiveShopOffer` deaktiviert das Angebot, ist idempotent und kann nicht
rückgängig gemacht werden. `EnableShopOffer` lehnt archivierte Angebote mit einem gezielten
Shop-Fehler ab; Storefront und Purchase prüfen den Archivierungszustand ausdrücklich. Die
vollständigen V1-Scope-Entscheidungen und der Abschlussaudit stehen in
[docs/architecture/shop.md](docs/architecture/shop.md).

## Integrations V1

Integrations V1 implementiert die persistierte External-Identity-Mapping- und
Resolution-Grenze. Eine kanonische Provider-Kennung und eine opaque externe
Benutzerkennung werden genau einer internen CommunityIdentityId zugeordnet. Externe
Kennungen ersetzen die zentrale Identity nie; unbekannte externe IDs erzeugen keine neue
Community-Identity.

Der Slice besitzt die Use Cases für Link, Resolve, Get, List und Unlink. Identisches
Linken ist idempotent, ein Reassignment zu einer anderen Community-Identity wird als
Konflikt abgelehnt. Identity-Existenz wird ausschließlich über den öffentlichen
Identity.Contracts-Contract geprüft. Die eigene Tabelle
integration_external_identity_mappings besitzt einen Primary Key auf
provider_key plus external_user_id und keinen Cross-Module-Foreign-Key.

Der API-Host registriert AddIntegrationsModule() und bietet die interne
Management-Grenze unter /api/admin/integrations/external-identities. Sie ist durch
Administration V1 permission- und CSRF-geschützt. Twitch OAuth/EventSub, Streamer.bot, OBS,
Razor/MVC, Sidebar, RBAC,
Notifications und Automation sind nicht Bestandteil dieses Slices; Streamer.bot bleibt
ein späterer externer Adapter. Details stehen in
[docs/architecture/integrations.md](docs/architecture/integrations.md).

## Lokale API-Ausführung

Voraussetzung sind das in `global.json` festgelegte stabile .NET-10-SDK und eine erreichbare PostgreSQL-Datenbank. Der API-Host führt die technische Migration-History sowie alle registrierten Identity-, Economy-, Inventory-, Shop-, Notifications-, Automation-, Overlay-, Integrations- und Messaging-Migrationen beim Start aus, darunter `Shop:4:AddShopOfferArchiveState`, `Notifications:1:CreateCommunityNotifications` und `Automation:1:CreateAutomationRulesAndExecutions`. Für lokale Zugangsdaten werden User Secrets oder Umgebungsvariablen verwendet; keine Passwörter gehören ins Repository.

```text
dotnet user-secrets set "ConnectionStrings:FlurNetz" "Host=localhost;Port=5432;Database=<datenbank>;Username=<benutzer>;Password=<passwort>" --project src/FlurNetz.Api
dotnet run --project src/FlurNetz.Api
```

Alternativ kann `ConnectionStrings__FlurNetz` als Umgebungsvariable gesetzt werden. Danach kann der Use Case ohne Request-Body aufgerufen werden:

```text
curl -i -X POST http://localhost:5000/api/identities
```

Die erfolgreiche Antwort hat den Status `201 Created` und die Form:

```json
{
  "id": "<erzeugte-guid>"
}
```

### Lokale Administration-Ersteinrichtung

Vor der ersten Anmeldung wird einmalig `GET/POST /admin/setup` verwendet. Dafür muss ein
Setup-Schlüssel ausschließlich über User-Secrets oder eine Umgebungsvariable gesetzt werden:

```text
dotnet user-secrets set "Administration:Setup:Secret" "<setup-schluessel>" --project src/FlurNetz.Api
```

Die Seite verlangt E-Mail-Adresse, Passwort, Bestätigung und diesen Schlüssel. Sie erzeugt
eine neue Identity, ein Administrator-Credential und die statische Administratorrolle atomar.
Nach dem ersten erfolgreichen Setup ist die Route geschlossen; die Anmeldung erfolgt unter
`/admin/login` mit der E-Mail-Adresse. `/admin/account` erlaubt anschließend die geschützte
Passwortänderung. Beide Passwortformulare bieten optional einen clientseitigen Generator mit
24 Zeichen auf Basis von `window.crypto.getRandomValues()`; der erzeugte Wert wird nicht in
Browser-Speichern, Cookies, Logs, Auditdaten oder Operations persistiert.

Der Entwicklungsstand enthält keine Live-Twitch-, Streamer.bot- oder sonstige
Plattformverbindung. Integrations V1 unterstützt lediglich das persistierte externe
Identity-Mapping.

## Lokale Worker-Ausführung

Der unabhängige Worker-Host verwendet dieselbe Konfigurationskonvention
ConnectionStrings:FlurNetz und benötigt eine erreichbare PostgreSQL-Datenbank. Beim Start führt
er ausschließlich die Messaging-, Progression-, Economy-, Notifications- und Automation-Migrationen aus, validiert die explizite
Registry-/Consumer-Komposition und verarbeitet die Outbox danach kontinuierlich. Die Registry
enthält zusätzlich `shop.purchase-completed` v1 aus `FlurNetz.Modules.Shop.Contracts`; die
Shop-Implementierung und Shop-Migrationen werden nicht referenziert beziehungsweise ausgeführt.
engagement_activities wird vom Worker nicht angelegt; der Worker referenziert nur die
benötigten Event-Contracts sowie Progression, Economy, Notifications und Automation. Der
Notifications-Consumer erzeugt für erfolgreiche Shop-Purchases eine persönliche Notification
atomar mit dem Inbox-Eintrag; Automation reserviert seine Execution und schreibt Credit- bzw.
Notification-Actions in derselben Transaktion. Bereits verarbeitete Nachrichten werden nicht
nachträglich backgefüllt.

Start:

    dotnet run --project src/FlurNetz.Worker

Der gestartete Processing-Loop bedeutet, dass Konfiguration, Startup-Migrationen und
Registry-/Handler-Komposition erfolgreich waren. Der Worker besitzt keinen HTTP-Health-Endpunkt,
keine Plattformintegration und keine eigene fachliche Retry- oder SQL-Logik. Details stehen in
[docs/architecture/worker.md](docs/architecture/worker.md).

## Gesamte lokale Prüfung

Für die vollständige Prüfung sind Docker für Testcontainers oder alternativ eine isolierte PostgreSQL-Datenbank über `FLURNETZ_TEST_CONNECTION_STRING` erforderlich.

```text
dotnet restore
dotnet build
dotnet test --solution FlurNetz.sln --no-build --no-restore
```

Die Architektur des Hosts ist in [docs/architecture/api.md](docs/architecture/api.md) beschrieben. Die initiale Gesamtrichtung steht in [docs/architecture/overview.md](docs/architecture/overview.md); die Regeln für BuildingBlocks stehen in [docs/architecture/building-blocks.md](docs/architecture/building-blocks.md).
