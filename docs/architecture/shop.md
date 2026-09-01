# Shop – Katalog, atomarer Inventory-Kauf, read-only Kaufhistorie und HTTP-Storefront

## Verantwortung

`FlurNetz.Modules.Shop` besitzt den fachlichen Angebotskatalog sowie die Shop-eigene
Kaufhistorie und Idempotenzgrenze. Ein Angebot verweist unveränderlich auf genau eine
`ItemDefinitionId` aus `Inventory.Contracts`; ein erfolgreicher Kauf dieses Slices gewährt
exakt eine Einheit dieser Item-Definition.

Shop besitzt weder Identity-, Economy- noch Inventory-Zustände. Diese Module werden beim Kauf
ausschließlich über schmale, caller-neutrale Contracts innerhalb derselben PostgreSQL-
Transaktion angesprochen. Es gibt kein globales Unit of Work und kein Cross-Module-SQL.

## Öffentliche Contracts

`FlurNetz.Modules.Shop.Contracts` enthält:

- `ShopOfferId`
- `ShopPurchaseId`
- `ShopPurchaseRequestId`
- `ShopPurchaseCompletedIntegrationEvent`

Alle drei IDs sind nicht-leere, Guid-basierte und unveränderliche Fachkennungen.
`ShopPurchaseId` wird serverseitig erzeugt. `ShopPurchaseRequestId` wird vom aufrufenden
Adapter bereitgestellt und ist die globale Idempotenzkennung eines Kaufrequests.

Das producer-owned Integration Event besitzt den stabilen logischen Typ
`shop.purchase-completed` und Schema-Version `1`. Die Payload enthält ausschließlich den
unveränderlichen historischen Kauf: Purchase-, Offer-, CommunityIdentity- und
ItemDefinition-ID, `PricePaid` und `PurchasedAtUtc`. Die Request-ID ist keine fachliche
Event-Payload; sie wird als technische Correlation-ID des Outbox-Envelopes verwendet.

`Shop.Contracts` referenziert ausschließlich `FlurNetz.Messaging`; fremde Domain- oder
Implementierungsassemblies werden nicht veröffentlicht.

Die Query-Typen `IShopPurchaseHistoryStore`, `GetShopPurchase`,
`ListShopPurchasesForIdentity`, `ShopPurchaseHistoryCursor` und
`ShopPurchaseHistoryPage` bleiben vollständig im Shop-Implementation-Assembly. Die
öffentliche Contract-Oberfläche wird für die read-only Kaufhistorie nicht erweitert.

## Angebotsdomain und Katalog

`ShopOffer` enthält `ShopOfferId`, `ItemDefinitionId`, den kanonisch getrimmten
`DisplayName` mit höchstens 200 Unicode-Skalarwerten, eine optionale nicht-leere
`Description` mit höchstens 2000 Unicode-Skalarwerten, `ShopPrice`, `IsEnabled`, das
halboffene `AvailabilityWindow` sowie ein optionales positives
`PurchaseLimitPerIdentity`.

U+0000 und nicht wohlgeformtes UTF-16 werden an der Domain-Grenze abgewiesen. Availability
verwendet `[AvailableFrom, AvailableUntil)`; gesetzte Grenzen sind kanonische UTC-Instants
mit exakt PostgreSQL-kompatibler Mikrosekundenpräzision.

`ShopOfferStore` persistiert den Katalog. Katalogmutationen laden genau das Zielangebot mit
`SELECT ... FOR UPDATE`, rehydrieren die Domain und schreiben nur tatsächliche Änderungen.
Ein No-op erzeugt kein `UPDATE`. Die unveränderlichen Ziel-IDs werden nie überschrieben.

Die internen Katalog-Use-Cases bleiben:

- `CreateShopOffer`
- `GetShopOffer`
- `ListShopOffers`
- `RenameShopOffer`
- `ChangeShopOfferDescription`
- `ChangeShopOfferPrice`
- `ChangeShopOfferAvailability`
- `ChangeShopOfferPurchaseLimit`
- `EnableShopOffer`
- `DisableShopOffer`

## Kaufdomain und Use Case

`ShopPurchase` ist ein unveränderlicher historischer Snapshot mit:

- `ShopPurchaseId`
- `ShopOfferId`
- `CommunityIdentityId`
- `ItemDefinitionId`
- `PricePaid`
- `PurchasedAtUtc`

Es gibt keinen Purchase-Status, keine veränderliche Menge und keinen nachträglichen Preisbezug.
Der Snapshot bleibt daher unabhängig von späteren Änderungen am Angebot.

`PurchaseShopOffer` nimmt `ShopPurchaseRequestId`, `ShopOfferId` und
`CommunityIdentityId` entgegen. Der Use Case erzeugt die `ShopPurchaseId` serverseitig,
liest die Zeit einmal über `IClock`, kanonisiert sie auf UTC mit PostgreSQL-
Mikrosekundenpräzision und delegiert an `IShopPurchaseExecutor`.

`IShopPurchaseExecutor` ist eine Shop-interne Application-Grenze. Sie enthält keine Dapper-,
Npgsql-, Connection-, Transaction- oder sonstigen technischen Datenbanktypen.

## Read-only Kaufhistorie

`GetShopPurchase` lädt einen einzelnen persistierten `ShopPurchase` über seine
`ShopPurchaseId`. Eine unbekannte ID liefert `null`; eine künstliche
`ShopPurchaseNotFoundException` gibt es für diesen Read nicht.

`ListShopPurchasesForIdentity` liest die Historie ausschließlich für die angefragte
`CommunityIdentityId`. Die verbindliche Reihenfolge ist
`purchased_at DESC, id DESC`. Die Application-Grenze verwendet eine Seitengröße von
`1` bis `100` mit Default `50` und ruft den Store für die gewünschte Seite plus einen
zusätzlichen Datensatz auf. Der zusätzliche Datensatz wird nicht ausgegeben, sondern
bestimmt `NextCursor`.

Die Pagination ist stabile Keyset-Pagination ohne Offset und ohne Gesamtzählung. Der
implementation-eigene `ShopPurchaseHistoryCursor` enthält
`CommunityIdentityId`, `PurchasedAtUtc` und `ShopPurchaseId`. Er ist an genau diese
Identity gebunden; ein Cursor einer anderen Identity wird vor dem Store-Zugriff
abgewiesen. Der Zeitpunkt ist UTC und besitzt exakt PostgreSQL-kompatible
Mikrosekundenpräzision. Die Seek-Bedingung lautet fachlich:

`purchased_at < cursor.PurchasedAtUtc`

oder bei gleichem Zeitpunkt:

`purchased_at = cursor.PurchasedAtUtc AND id < cursor.ShopPurchaseId`.

Der gezielte `ShopPurchaseHistoryStore` liest ausschließlich `shop_purchases` mit der
vorhandenen `IPostgreSqlConnectionFactory` und Dapper und rehydriert über
`ShopPurchase.Rehydrate(...)`. Jede History-Abfrage besteht aus genau einem normalen
Read ohne zusätzliche PostgreSQL-Transaktion, `FOR UPDATE`, `FOR SHARE`, fachliche Locks
oder Guard-Tabelle. Es gibt keinen Snapshot über mehrere Pagination-Seiten; zwischen zwei
Requests dürfen neue Käufe committed werden. Eine unbekannte oder historisch leere
Identity liefert eine leere Seite und wird nicht über `ICommunityIdentityExistence`
geprüft.

## Read-only Storefront API

Der Shop ist erstmals über `FlurNetz.Api` read-only per HTTP erreichbar. Die API registriert
gezielt `AddShopReadOnlyModule()` und verwendet deshalb nur die bestehenden Read-Use-Cases und
Stores:

- `GET /api/shop/offers` listet ausschließlich aktivierte Angebote, die zum einmal ermittelten
  aktuellen Zeitpunkt in ihrem `AvailabilityWindow` liegen.
- `GET /api/shop/offers/{offerId}` liefert nur ein existierendes und aktuell sichtbares Angebot;
  unbekannte, deaktivierte, zukünftige und abgelaufene Angebote liefern `404 Not Found`.
- `GET /api/shop/purchases/{purchaseId}` liefert den vollständigen historischen Snapshot oder
  `404 Not Found`.
- `GET /api/shop/identities/{communityIdentityId}/purchases` liefert die bestehende,
  identity-isolierte History mit `pageSize` (Default `50`, `1` bis `100`) und Cursor.

Die API verwendet ausschließlich API-eigene DTOs mit JSON-Primitives; Domainobjekte und
Contract-Value-Types werden nicht direkt serialisiert. Der History-Cursor ist ein API-eigener
opaque Cursor: UTF-8-JSON mit interner Version `1`, den Feldern `communityIdentityId`,
`purchasedAtUtc` und `shopPurchaseId`, anschließend Base64Url-kodiert. Der Cursor wird bei jedem
Request strikt dekodiert, validiert und gegen die Route-Identity gebunden. Fehlerhafte,
unvollständige, unbekannt versionierte oder Identity-fremde Cursor liefern `400 Bad Request`.

Die API registriert keinen `IShopPurchaseExecutor`, kein `PurchaseShopOffer` und keine
Katalogmutation. Es existiert weiterhin kein HTTP-Purchase; der interne atomare Purchase bleibt
unverändert vorhanden. Der API-Host registriert weiterhin ausschließlich
`AddShopReadOnlyModule()`. Der separate Worker kennt `shop.purchase-completed` v1 über
`FlurNetz.Modules.Shop.Contracts`, registriert aber aktuell bewusst keinen fachlichen
Shop-Consumer; dieses Contract-Wiring erzeugt keinen HTTP-Write-Pfad. Ein HTTP-Purchase folgt
erst in einem separaten späteren Slice.

## Minimale Cross-Module-Capabilities

Für den atomaren Kauf existieren genau drei schmale Contracts:

- `Identity.Contracts.ICommunityIdentityExistence`
- `Economy.Contracts.IEconomyBalanceDebit`
- `Inventory.Contracts.IInventoryQuantityGrant`

Alle verwenden ausschließlich fachliche IDs beziehungsweise Beträge und neutrale
`DbConnection`-/`DbTransaction`-Basistypen. Die implementierenden Module kennen Shop nicht.

Identity prüft nur die Existenz der bereits aufgelösten internen Identität. Economy führt
denselben Domain- und Row-Lock-Debit wie sein normaler Debit-Pfad aus, aber ohne eigenen
Commit. Inventory verwendet denselben Domain- und Sparse-Lifecycle wie sein normaler Add-Pfad
und gewährt im Shop-Slice exakt die Menge `1`.

Shop referenziert keine fremde Modulimplementierung und schreibt niemals direkt in
`community_identities`, `community_economies` oder `community_inventory_entries`.

## Atomare Kauftransaktion

`PostgreSqlShopPurchaseExecutor` besitzt die vollständige fachliche Transaktionsgrenze:

1. `PostgreSqlTransaction` beginnen.
2. `ShopPurchaseRequestId` in `shop_purchase_requests` reservieren.
3. Bei bestehendem identischem Request den bereits persistierten Kauf laden und zurückgeben.
4. Bei gleicher Request-ID mit anderer Identity oder anderem Offer
   `ShopPurchaseIdempotencyConflictException` auslösen.
5. Existenz der `CommunityIdentityId` über Identity innerhalb derselben Connection und
   Transaction prüfen.
6. Das Angebot mit `FOR SHARE` laden und rehydrieren.
7. Aktivierung und `AvailabilityWindow` gegen den einmal bestimmten Kaufzeitpunkt prüfen.
8. Falls ein Kauflimit gesetzt ist, den Guard für
   `(shop_offer_id, community_identity_id)` lazy anlegen, mit `FOR UPDATE` sperren und
   bereits persistierte Käufe zählen.
9. Den gesperrten aktuellen Preis als `PricePaid` snapshotten.
10. Bei `PricePaid > 0` Economy über `IEconomyBalanceDebit` abbuchen.
11. Inventory über `IInventoryQuantityGrant` um exakt `1` erhöhen.
12. Den unveränderlichen `ShopPurchase` persistieren.
13. `shop.purchase-completed` v1 über den bestehenden Outbox-Publisher in derselben
    Transaktion enqueuen.
14. Erst danach committen.

`FOR SHARE` erlaubt parallele Käufer desselben Angebots, verhindert aber, dass eine
Katalogmutation mit `FOR UPDATE` Preis, Aktivierung, Availability oder Limit mitten durch
einen Purchase-Snapshot verändert.

Bei jedem Fehler werden Request-Reservation, Guard-Anlage, Economy-Debit, Inventory-Grant,
Purchase-Write und Outbox gemeinsam zurückgerollt. Das gilt auch für Cancellation,
unzureichenden Saldo, Inventory-Overflow, unbekannte Identity, unbekanntes oder nicht
verfügbares Offer und überschrittenes Kauflimit.

## Idempotenz und Kauflimit

`shop_purchase_requests.request_id` ist die authoritative Idempotenzgrenze. Die Reservation
enthält zusätzlich den vorab erzeugten `shop_purchase_id`, `shop_offer_id` und
`community_identity_id`.

Ein erfolgreich wiederholter identischer Request liefert denselben persistierten Kauf. Er
erzeugt weder einen zweiten Economy-Debit noch einen zweiten Inventory-Grant, Purchase oder
Outbox-Eintrag. Eine abweichende Wiederverwendung derselben Request-ID ist ein expliziter
Conflict.

Die Guard-Tabelle besitzt den Composite Primary Key
`(shop_offer_id, community_identity_id)`. Dadurch werden nur konkurrierende Käufe derselben
Identität für dasselbe Angebot serialisiert. Das Kauflimit zählt ausschließlich erfolgreiche,
persistierte `shop_purchases`; fehlgeschlagene Versuche und zurückgerollte Requests zählen
nicht.

## PostgreSQL-Migrationen

### Shop:1:CreateShopOffers

Die unveränderte V1-Migration besitzt ausschließlich `shop_offers` mit:

- `id uuid PRIMARY KEY`
- `item_definition_id uuid NOT NULL`
- `display_name varchar(200) NOT NULL`
- `description varchar(2000) NULL`
- `price bigint NOT NULL`
- `is_enabled boolean NOT NULL`
- `available_from timestamptz NULL`
- `available_until timestamptz NULL`
- `purchase_limit_per_identity integer NULL`

Es gibt keine Cross-Module-Foreign-Keys.

### Shop:2:CreateShopPurchases

V2 ergänzt drei Shop-eigene Tabellen.

`shop_purchases`:

- `id uuid PRIMARY KEY`
- `shop_offer_id uuid NOT NULL`
- `community_identity_id uuid NOT NULL`
- `purchased_inventory_item_definition_id uuid NOT NULL`
- `price_paid bigint NOT NULL CHECK (price_paid >= 0)`
- `purchased_at timestamptz NOT NULL`

Der einzige Foreign Key ist Shop-intern von `shop_purchases.shop_offer_id` auf
`shop_offers.id` mit restriktivem Delete-Verhalten. Identity, Economy und Inventory werden
nicht über relationale Cross-Module-FKs gekoppelt.

Zusätzliche Indizes:

- `(shop_offer_id, community_identity_id)`
- `(community_identity_id, purchased_at)`

`shop_purchase_requests`:

- `request_id uuid PRIMARY KEY`
- `shop_purchase_id uuid NOT NULL UNIQUE`
- `shop_offer_id uuid NOT NULL`
- `community_identity_id uuid NOT NULL`

`shop_purchase_guards`:

- `shop_offer_id uuid NOT NULL`
- `community_identity_id uuid NOT NULL`
- Composite Primary Key über beide Spalten.

Für die Kaufhistorie wird keine weitere Migration, Tabelle oder Pagination-Struktur
angelegt. Der vorhandene Index `(community_identity_id, purchased_at)` wird für die
Keyset-Abfrage weiterverwendet; `Shop:1:CreateShopOffers` und `Shop:2:CreateShopPurchases`
bleiben unverändert.

## Outbox und Runtime

Der Shop-Purchase verwendet den vorhandenen `IIntegrationEventPublisher`. Dieser öffnet keine
eigene Verbindung und committed nicht selbst. Business-Write und Outbox-Eintrag werden deshalb
durch denselben PostgreSQL-Commit sichtbar.

Der Shop-Purchase veröffentlicht das Event weiterhin ausschließlich als atomaren Outbox-
Bestandteil des Kaufs. Slice 6 registriert `shop.purchase-completed` v1 im separaten Worker
explizit über `Shop.Contracts`, ohne die Shop-Implementierung oder Shop-Migrationen zu
referenzieren. Es existiert weiterhin kein fachlicher Shop-Event-Consumer. Der bekannte Eventtyp
wird vom Worker nach erfolgreicher Deserialisierung ohne Handler und ohne Inbox-Eintrag als
`processed` markiert; er wird weder als Retry/Poison behandelt noch über bereits verarbeitete
Outbox-Nachrichten später replaybar. Die bestehende Engagement→Progression-Runtime bleibt
unverändert.

## Modulregistrierung und Abhängigkeiten

`ShopModule.AddShopReadOnlyModule(...)` registriert eine neutrale `IClock`-Default-
Implementierung per `TryAddSingleton`, beide Stores, die vollständigen Katalog- und History-
Reads, `GetAvailableShopOffer`, `ListAvailableShopOffers` sowie `ShopMigrationSource`. Diese
Registration umfasst zehn Services und enthält keinen Purchase-Executor und keine Mutation.
`ShopModule.AddShopModule(...)` verwendet diese Read-Basis und ergänzt weiterhin alle
Katalogmutationen, `IShopPurchaseExecutor` und `PurchaseShopOffer`; der vollständige Umfang
umfasst damit 20 Services.

Messaging-Registry, Serializer, `IIntegrationEventPublisher`, Connection Factory, API- und
Worker-Komposition bleiben außerhalb des Shop-Moduls. Der API-Host bindet die Read-only-
Registration ein und führt dadurch die Identity- sowie beide vorhandenen Shop-Migrationen aus.
Der Worker referenziert für das Contract-Wiring ausschließlich `Shop.Contracts`; er registriert
keine `ShopMigrationSource` und führt keine Shop-Migration aus.

Erlaubte FlurNetz-Abhängigkeiten der Shop-Implementierung sind ausschließlich:

- `FlurNetz.BuildingBlocks`
- `FlurNetz.Messaging`
- `FlurNetz.Modules.Shop.Contracts`
- `FlurNetz.Modules.Identity.Contracts`
- `FlurNetz.Modules.Economy.Contracts`
- `FlurNetz.Modules.Inventory.Contracts`
- `FlurNetz.Persistence`

## Tests und bewusste Nicht-Ziele

Unit- und Architekturtests sichern IDs, Event-Schema, immutable Purchase-Snapshots,
Zeitkanonisierung, Purchase-History-Cursor und -Seitengröße, DI-Scope, Reference Graph
und die drei Cross-Module-Capabilities.

Die PostgreSQL-Integrationstests prüfen zusätzlich:

- beide Shop-Migrationen und deren Idempotenz;
- den vollständigen Shop-Relationsumfang;
- den bestehenden Katalog einschließlich Unicode-, Zeit-, Rollback- und Concurrency-Garantien;
- erfolgreichen Kauf mit gemeinsamem Economy-, Inventory-, Purchase-, Request-, Guard- und
  Outbox-Commit;
- kostenlosen Kauf ohne Economy-Zeile;
- parallele identische Requests mit exakt einem Effekt und derselben Purchase-ID;
- Idempotency-Conflict bei abweichendem Offer oder abweichender Identity;
- sichtbar fehlschlagende korrupte Request→Purchase-Abbildungen ohne erneute Business-Effekte;
- konkurrierendes Kauflimit sowie parallele unbegrenzte Käufe ohne Guard;
- unbekannte Identity und unbekanntes Offer;
- deaktivierte und außerhalb ihres Availability-Fensters liegende Offers;
- Rollback bei unzureichendem Economy-Saldo;
- Rollback eines bereits ausgeführten Debits bei Inventory-Overflow;
- Rollback aller Business-Writes bei Outbox-Fehler;
- unveränderliche historische Purchase-Snapshots nach späteren Katalogänderungen;
- den tatsächlich beobachteten PostgreSQL-Lock-Wait einer Katalogmutation während eines
  laufenden Purchase-Snapshots.
- den vollständigen Purchase-Lookup einschließlich aller Snapshot-Felder;
- die isolierte, newest-first Kaufhistorie pro Identity;
- deterministische Reihenfolge bei identischen `purchased_at`-Zeitpunkten über `id DESC`;
- mehrseitige Keyset-Pagination ohne Duplikate und ohne ausgelassene Käufe;
- `NextCursor` auf der letzten Seite und die leere History;
- den echten API-Host mit Offer-Storefront, DTO-Abbildung, Purchase-Lookup und History-Cursor;
- ungültige Route-IDs, Page Sizes und malformed, ungültige oder Identity-fremde API-Cursor;
- einen zwischen Seiten neu persistierten, zeitlich neueren Kauf ohne Rückwärtsbewegung
  des bereits ausgegebenen Cursors.

Die Unit Tests prüfen zusätzlich den vollständigen Serialize-/Deserialize-Roundtrip von
`shop.purchase-completed` v1 über die bestehende explizite Messaging-Registry.

Die Worker-Integration prüft zusätzlich die Verarbeitung des bekannten Events durch den echten
Worker ohne fachlichen Consumer und ohne Inbox-Eintrag. Nicht enthalten sind HTTP-Purchase,
Admin API/UI, fachlicher Shop-Event-Consumer, Warenkorb, variable Purchase-Menge, Stock,
Discounts, Coupons, Refunds, Purchase-Cancellation, Ledger, Saga/Compensation,
Distributed Transactions, globale Unit-of-Work-Abstraktionen, generische Repositories,
generische Pagination-Foundations, Inventory-Item-Instanzen, Titles-/Rewards-/
Achievement-Ausführung oder eine generische `OfferTarget`-Abstraktion.
