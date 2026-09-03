# Shop – vollständiger FlurNetz-V1-Katalog, atomarer Purchase, Kaufhistorie und HTTP-API

## Verantwortung

`FlurNetz.Modules.Shop` besitzt den fachlichen Angebotskatalog sowie die Shop-eigene
Kaufhistorie und Idempotenzgrenze. Ein Angebot verweist unveränderlich auf genau eine
`ItemDefinitionId` aus `Inventory.Contracts`; ein erfolgreicher Kauf im Shop-V1 gewährt
exakt eine Einheit dieser Item-Definition.

Shop besitzt weder Identity-, Economy- noch Inventory-Zustände. Diese Module werden beim Kauf
ausschließlich über schmale, caller-neutrale Contracts innerhalb derselben PostgreSQL-
Transaktion angesprochen. Es gibt kein globales Unit of Work und kein Cross-Module-SQL.

## Verbindlicher Shop-V1-Scope

Der Shop-V1-Scope ist nach dem vollständigen Ist-/Gap-Audit bewusst klein und direkt. Die
folgende Tabelle hält die Entscheidungen für die aktuell vorgesehene FlurNetz-Nutzung fest:

| Bereich | V1-Entscheidung | Konsequenz |
| --- | --- | --- |
| Kaufmenge | Nicht erforderlich | Ein Purchase gewährt verbindlich genau eine Inventory-Einheit. Mehrere Einheiten können über mehrere unabhängige Requests gekauft werden; jeder erfolgreiche Request besitzt weiterhin eigene Idempotenz-, History- und Event-Semantik. |
| Globaler Angebotsbestand | Nicht erforderlich | Der Shop verkauft virtuelle Inventory-Einheiten ohne Shop-eigene Knappheit. Die Community-Bestände bleiben Inventory-owned. Begrenzter Stock ist ein späteres, additives Feature. |
| Kategorien/Katalogstruktur | Nicht erforderlich | Die flache Katalogliste mit stabiler `sort_order ASC, id ASC`-Reihenfolge deckt den V1-Lesefall ab. Es gibt keine Kategorieidentitäten und keine UI-Dekorationsfelder. |
| Shop-Metadaten | Teilweise erforderlich und vorhanden | `DisplayName` und optionale `Description` sind persistiert, validiert und administrierbar. Badge-, Bild- und weitere Metadaten sowie Asset-Hosting haben keinen belegten V1-Nutzen. |
| Zeitpreise/Discounts/Coupons | Nicht erforderlich | Der aktuelle Preis und das Availability-Fenster sind ausreichend. Es gibt keine verdeckte oder zeitgesteuerte Rabattlogik und keine Coupon-Einlösung. |
| Warenkorb | Nicht erforderlich | Der verbindliche V1-Kauf ist ein direkter Offer-Purchase. Ein Cart würde ohne Mehrangebots- oder Checkout-Anforderung nur zusätzliche Transaktions- und Idempotenzsemantik einführen. |
| Refund/Cancellation | Nicht erforderlich | Erfolgreiche Purchases bleiben unveränderliche Historie. Eine Rückabwicklung müsste Economy, Inventory, History und gegebenenfalls Messaging gemeinsam kompensieren; dafür gibt es im vorgesehenen V1-Einsatz keinen Bedarf. |
| `shop.purchase-completed`-Consumer | Kein Shop-owned Consumer erforderlich | Das Event wird im Worker durch das eigenständige Notifications-Modul konsumiert. Shop bleibt ausschließlich Eigentümer von Purchase und Event und kennt keine Notification-Implementierung. |
| Authentication/Authorization | Nicht Shop-owned | Die Management-Grenze ist durch das systemweite Administration-Cookie-Scheme, explizite `Shop.Read`/`Shop.Manage`-Policies, Anti-Forgery, Audit und Operations geschützt. Shop besitzt die Security nicht. |

Damit sind `ShopOfferId`, `ItemDefinitionId`, Anzeigename, Beschreibung, Preis, Availability,
Kauflimit, SortOrder, Aktivierung und terminale Archivierung die vollständige Shop-owned
Katalogkonfiguration für V1. Diese Werte sind über die Management-Grenze steuerbar; feste
Domain- und Technikregeln wie Einzelstück-Semantik, UTC-/Mikrosekundenkanonisierung,
Idempotenzschlüssel und Katalogreihenfolge sind keine Laufzeitkonfiguration.

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
`Description` mit höchstens 2000 Unicode-Skalarwerten, `ShopPrice`, `IsEnabled`, `IsArchived`,
das halboffene `AvailabilityWindow` sowie ein optionales positives
`PurchaseLimitPerIdentity` und den nicht-negativen `SortOrder`.

`SortOrder` muss größer oder gleich `0` sein. Neue Angebote verwenden standardmäßig `0`;
explizit gesetzte positive Werte und mehrere gleiche Werte sind gültig. Es gibt keine
automatische Umnummerierung, keine lückenlose Positionspflicht und keinen eigenen Value-Type.
Die verbindliche Katalogreihenfolge lautet `sort_order ASC, id ASC`, wobei `ShopOfferId` nur
der deterministische Tie-Breaker ist. `ChangeSortOrder(int)` ändert nur einen abweichenden
Wert und liefert bei identischem Wert `false`; negative Werte werden als Argumentfehler
abgewiesen. `ShopOfferId` und `ItemDefinitionId` bleiben unveränderlich.

Neue Angebote starten mit `IsEnabled = false` und `IsArchived = false`. `Archive()` ist eine
terminale Domainmutation: Die erste Archivierung setzt `IsArchived = true` und `IsEnabled =
false` und liefert `true`; jede weitere Archivierung ist ein No-op und liefert `false`. Ein
archiviertes Angebot kann nicht reaktiviert werden. `Enable()` wirft dafür den gezielten
`ShopOfferArchivedException`.

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
- `ChangeShopOfferSortOrder`
- `EnableShopOffer`
- `DisableShopOffer`
- `ArchiveShopOffer`

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

## Storefront-, Management- und Purchase-API

Der Shop ist über `FlurNetz.Api` per HTTP erreichbar. Die API registriert dafür das vollständige
`AddShopModule()`: Dadurch sind der bestehende atomare Purchase-Use-Case und seine Persistence-
Abhängigkeiten verfügbar; die Katalogverwaltung wird über eine eigene Endpoint-Gruppe klar von
der öffentlichen Storefront getrennt.
Die Storefront-Reads verwenden weiterhin die bestehenden Read-Use-Cases und Stores:

- `GET /api/shop/offers` listet ausschließlich Angebote nach der Regel
  `IsEnabled && !IsArchived && IsAvailableAt(now)`. Mehrere sichtbare Angebote werden
  in der vom Store gelieferten fachlichen Reihenfolge `sort_order ASC, id ASC` ausgegeben.
- `GET /api/shop/offers/{offerId}` liefert nur ein existierendes und aktuell sichtbares Angebot;
  unbekannte, deaktivierte, archivierte, zukünftige und abgelaufene Angebote liefern `404 Not Found`.
- `GET /api/shop/purchases/{purchaseId}` liefert den vollständigen historischen Snapshot oder
  `404 Not Found`.
- `GET /api/shop/identities/{communityIdentityId}/purchases` liefert die bestehende,
  identity-isolierte History mit `pageSize` (Default `50`, `1` bis `100`) und Cursor.
- `POST /api/shop/offers/{offerId}/purchases` führt den bestehenden `PurchaseShopOffer` für ein
  kaufbares Angebot aus.

Die API verwendet ausschließlich API-eigene DTOs mit JSON-Primitives; Domainobjekte und
Contract-Value-Types werden nicht direkt serialisiert. Der History-Cursor ist ein API-eigener
opaque Cursor: UTF-8-JSON mit interner Version `1`, den Feldern `communityIdentityId`,
`purchasedAtUtc` und `shopPurchaseId`, anschließend Base64Url-kodiert. Der Cursor wird bei jedem
Request strikt dekodiert, validiert und gegen die Route-Identity gebunden. Fehlerhafte,
unvollständige, unbekannt versionierte oder Identity-fremde Cursor liefern `400 Bad Request`.

Der POST-Adapter besitzt ausschließlich HTTP-Verantwortung: Er validiert Route und Request,
bildet die vorhandenen `ShopOfferId`, `ShopPurchaseRequestId` und `CommunityIdentityId` und ruft
`PurchaseShopOffer.ExecuteAsync(...)` auf. Der API-eigene Request enthält nur `requestId` und
`communityIdentityId`; `offerId` bleibt Route-Parameter. Bei Erfolg kommt der bestehende
`ShopPurchaseResponse` mit `201 Created` und Location
`/api/shop/purchases/{purchaseId}` zurück. Derselbe Request wird über
`ShopPurchaseRequestId` mit derselben Purchase-ID und Location idempotent beantwortet; ein
Replay-Flag ist nicht Bestandteil des Vertrags.

Ungültige Route-/Request-Identifier und malformed JSON liefern `400 Bad Request`, unbekanntes
Offer oder unbekannte Identity `404 Not Found`, nicht kaufbares Offer, Kauflimit,
Idempotenzkonflikt und unzureichender Saldo `409 Conflict`; alle bekannten Antworten sind
ProblemDetails. Sonstige, insbesondere technische `InvalidOperationException`-Fälle, bleiben
`500 Internal Server Error`. Der separate Worker kennt `shop.purchase-completed` v1 über
`FlurNetz.Modules.Shop.Contracts`; er registriert dafür den Notifications-Consumer
`notifications.shop-purchase`, aber keinen Shop-owned Consumer.

### HTTP-Management-Grenze für den Angebotskatalog

Die Katalogverwaltung verwendet ausschließlich die bereits registrierten Application-Use-Cases
`CreateShopOffer`, `GetShopOffer`, `ListShopOffers`, `RenameShopOffer`,
`ChangeShopOfferDescription`, `ChangeShopOfferPrice`, `ChangeShopOfferAvailability`,
`ChangeShopOfferPurchaseLimit`, `ChangeShopOfferSortOrder`, `EnableShopOffer`,
`DisableShopOffer` und `ArchiveShopOffer`:

```text
GET  /api/admin/shop/offers
GET  /api/admin/shop/offers/{offerId}
POST /api/admin/shop/offers
PUT  /api/admin/shop/offers/{offerId}/display-name
PUT  /api/admin/shop/offers/{offerId}/description
PUT  /api/admin/shop/offers/{offerId}/price
PUT  /api/admin/shop/offers/{offerId}/availability
PUT  /api/admin/shop/offers/{offerId}/purchase-limit
PUT  /api/admin/shop/offers/{offerId}/sort-order
POST /api/admin/shop/offers/{offerId}/enable
POST /api/admin/shop/offers/{offerId}/disable
POST /api/admin/shop/offers/{offerId}/archive
```

Der Create-Request enthält die bestehenden fachlichen Werte `itemDefinitionId`, `displayName`,
optionale `description`, `price`, optionale Availability-Grenzen und ein optionales
`purchaseLimitPerIdentity` und `sortOrder`; fehlt `sortOrder`, wird `0` verwendet. Negative
Werte liefern `400 ProblemDetails`. Die ID vergibt der Use-Case serverseitig. Ein neues Angebot
bleibt deaktiviert. Die Management-Lesesicht besitzt ein eigenes API-Response-DTO inklusive
`IsEnabled`, `IsArchived` und `SortOrder` und verwendet nicht den Storefront-Vertrag. Deshalb kann sie den
vollständigen internen Katalog einschließlich deaktivierter, zukünftiger und abgelaufener
Angebote sowie archivierter Angebote sehen. Die Management-Liste folgt ebenfalls
`sort_order ASC, id ASC`.

`PUT /api/admin/shop/offers/{offerId}/sort-order` verwendet den API-eigenen
`ChangeShopOfferSortOrderRequest`. Ein gültiger Wert und ein fachlicher No-op liefern `204 No
Content`; unbekannte gültige IDs liefern `404 ProblemDetails`, ungültige IDs, fehlende oder
malformed Bodies sowie negative Werte `400 ProblemDetails`.

`POST /api/admin/shop/offers/{offerId}/archive` verwendet `ArchiveShopOffer`, hat keinen
Request-Body und liefert bei erstmaliger wie wiederholter Archivierung `204 No Content`.
Unbekannte gültige IDs liefern `404 ProblemDetails`, ungültige IDs `400 ProblemDetails`; ein
späteres Enable eines archivierten Angebots liefert `409 Conflict` als ProblemDetails.

Der HTTP-Adapter öffnet keine eigene Transaktion und greift nicht direkt auf Store, Dapper,
Npgsql oder Tabellen zu. Die vorhandene `ShopOfferStore.ExecuteAsync`-Grenze mit
`SELECT FOR UPDATE` bleibt unverändert. Erfolgreiche Mutationen liefern `204 No Content`;
unbekannte Offer-IDs `404 Not Found`, bekannte ungültige Werte und malformed JSON `400 Bad
Request`, jeweils als ProblemDetails. Wiederholtes Enable oder Disable bleibt ein No-op.
Die Storefront bleibt ausschließlich auf `IsEnabled && !IsArchived && IsAvailableAt(now)`
beschränkt.
Die Management-Routen sind durch Administration V1 permission- und Anti-Forgery-geschützt;
High-Risk-Aktionen verlangen Reason und RequestId und werden über Audit/Operations atomar
mit der Shop-Mutation behandelt. Der Shop besitzt diese Security nicht selbst.
Der API-Adapter führt Migrationen nicht selbst aus; `Shop:4:AddShopOfferArchiveState` wird wie
die übrigen Shop-Migrationen über `ShopMigrationSource` registriert. Die Management-Grenze
veröffentlicht keine Events und registriert keinen Shop-owned Consumer; der separate Worker
verarbeitet das unveränderte Purchase-Event über Notifications.

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
und gewährt im Shop-V1 exakt die Menge `1`.

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
7. Archivierung, Aktivierung und `AvailabilityWindow` gegen den einmal bestimmten Kaufzeitpunkt
   prüfen; archivierte Angebote sind ausdrücklich nicht kaufbar.
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
Katalogmutation mit `FOR UPDATE` Preis, Archivierung, Aktivierung, Availability oder Limit mitten
durch einen Purchase-Snapshot verändert. Gewinnt der Purchase zuerst, wartet die Archivierung,
bis der konsistent begonnene Kauf committet ist, und archiviert danach. Gewinnt die Archivierung
zuerst, liest ein danach gestarteter Purchase den archivierten und deaktivierten Zustand und wird
vor allen Economy-, Inventory-, Purchase-, Reservation-, Guard- oder Outbox-Wirkungen
zurückgerollt abgewiesen.

Bei jedem Fehler werden Request-Reservation, Guard-Anlage, Economy-Debit, Inventory-Grant,
Purchase-Write und Outbox gemeinsam zurückgerollt. Das gilt auch für Cancellation,
unzureichenden Saldo, Inventory-Overflow, unbekannte Identity, unbekanntes oder nicht
verfügbares Offer, archiviertes Offer und überschrittenes Kauflimit.

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

Die Migration `Shop:2:CreateShopPurchases` ergänzt drei Shop-eigene Tabellen.

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

### Shop:3:AddShopOfferSortOrder

Die unveränderte V1-Tabelle `shop_offers` wird ausschließlich um
`sort_order integer NOT NULL` erweitert. Die Migration verwendet beim Hinzufügen temporär
`DEFAULT 0`, damit alle bestehenden Angebote fachlich mit `SortOrder = 0` backfillt werden,
setzt danach `NOT NULL`, entfernt den Default wieder und ergänzt ausschließlich den Check
`sort_order >= 0`. Es gibt keinen permanenten Default, keinen Index, keinen Unique Constraint,
keinen Foreign Key und keine weitere Tabelle. Die vorherigen Migrationen werden inhaltlich nicht
verändert.

Für die Kaufhistorie wird keine weitere Migration, Tabelle oder Pagination-Struktur
angelegt. Der vorhandene Index `(community_identity_id, purchased_at)` wird für die
Keyset-Abfrage weiterverwendet; `Shop:1:CreateShopOffers` und `Shop:2:CreateShopPurchases`
bleiben unverändert. `SortOrder` gehört nicht zu `ShopPurchase`, zum Purchase-Snapshot oder
zu `shop.purchase-completed` v1.

### Shop:4:AddShopOfferArchiveState

Die bestehende Tabelle `shop_offers` wird ausschließlich um
`is_archived boolean NOT NULL` erweitert. Ein temporärer `DEFAULT false` backfillt bestehende
Angebote; danach wird der Default entfernt. Die Migration ergänzt ausschließlich den Check
`CHECK (NOT (is_archived AND is_enabled))`. Es gibt keine weitere Tabelle, keinen Index und
keinen Foreign Key. `Shop:1:CreateShopOffers`, `Shop:2:CreateShopPurchases` und
`Shop:3:AddShopOfferSortOrder` bleiben unverändert; historische Purchases werden nicht
aktualisiert.

## Outbox und Runtime

Der Shop-Purchase verwendet den vorhandenen `IIntegrationEventPublisher`. Dieser öffnet keine
eigene Verbindung und committed nicht selbst. Business-Write und Outbox-Eintrag werden deshalb
durch denselben PostgreSQL-Commit sichtbar.

Der Shop-Purchase veröffentlicht das Event weiterhin ausschließlich als atomaren Outbox-
Bestandteil des Kaufs. Der separate Worker registriert `shop.purchase-completed` v1 explizit
über `Shop.Contracts`, ohne die Shop-Implementierung oder Shop-Migrationen zu referenzieren.
Das eigenständige Notifications-Modul verarbeitet den Eventtyp als Consumer
`notifications.shop-purchase`; Shop besitzt weiterhin keinen eigenen Event-Consumer. Die
Notification und der Inbox-Eintrag werden in der Processor-Transaktion gemeinsam persistiert.
Die bestehende Engagement→Progression-Runtime bleibt unverändert.

Der API-Host ist zusätzlich ein Producer für dieses Event. Er registriert explizit nur
`ShopPurchaseCompletedIntegrationEvent` v1 über die Contract-Konstanten, den vorhandenen
`IntegrationEventJsonSerializer`, `IIntegrationEventPublisher` als
`PostgreSqlOutboxPublisher` und `MessagingMigrationSource`. Die vom `AddShopModule()`-Wiring
bereitgestellte `IClock` wird wiederverwendet. Der API-Prozess startet keinen
`OutboxProcessor`, keinen Messaging-Worker und keinen Inbox-Consumer; eine erfolgreiche HTTP-
Purchase hinterlässt die Nachricht daher `pending`, bis der separate Worker sie verarbeitet.

## Modulregistrierung und Abhängigkeiten

`ShopModule.AddShopReadOnlyModule(...)` registriert eine neutrale `IClock`-Default-
Implementierung per `TryAddSingleton`, beide Stores, die vollständigen Katalog- und History-
Reads, `GetAvailableShopOffer`, `ListAvailableShopOffers` sowie `ShopMigrationSource`. Diese
Registration umfasst zehn Services und enthält keinen Purchase-Executor und keine Mutation.
`ShopModule.AddShopModule(...)` verwendet diese Read-Basis und ergänzt weiterhin alle
Katalogmutationen, `IShopPurchaseExecutor` und `PurchaseShopOffer`; der vollständige Umfang
umfasst damit 22 Services. `AddShopReadOnlyModule(...)` bleibt bei zehn Services und
registriert weiterhin keine Katalogmutation.

Messaging-Registry, Serializer, `IIntegrationEventPublisher`, Connection Factory, API- und
Worker-Komposition bleiben außerhalb des Shop-Moduls. Der API-Host bindet `AddShopModule()`
zusammen mit der Identity- sowie den schmalen Economy-/Inventory-Capabilities ein und führt
dadurch die zehn Identity-, Economy-, Inventory-, Shop-, Notifications-, Automation- und Messaging-Migrationen aus,
einschließlich `Shop:4:AddShopOfferArchiveState`. Die internen Katalogmutationen sind registriert
und werden vom API-Host über
die getrennte Management-Endpoint-Gruppe auf die bestehenden Use-Cases abgebildet.
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

## Tests und bewusst ausgeschlossene V1-Funktionen

Unit- und Architekturtests sichern IDs, Event-Schema, immutable Purchase-Snapshots,
Zeitkanonisierung, Purchase-History-Cursor und -Seitengröße, DI-Scope, Reference Graph
und die drei Cross-Module-Capabilities.

Die PostgreSQL-Integrationstests prüfen zusätzlich:

- alle vier Shop-Migrationen und deren Idempotenz einschließlich der V3-/V4-Backfills und der
  fehlenden permanenten Spalten-Default;
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
- deaktivierte, archivierte und außerhalb ihres Availability-Fensters liegende Offers;
- Rollback bei unzureichendem Economy-Saldo;
- Rollback eines bereits ausgeführten Debits bei Inventory-Overflow;
- Rollback aller Business-Writes bei Outbox-Fehler;
- unveränderliche historische Purchase-Snapshots nach späteren Katalogänderungen;
- den tatsächlich beobachteten PostgreSQL-Lock-Wait einer Katalogmutation während eines
  laufenden Purchase-Snapshots.
- die deterministische Lock-Reihenfolge zwischen zuerst laufender Archivierung und Purchase
  einschließlich vollständigem Rollback des abgewiesenen Purchases;
- den vollständigen Purchase-Lookup einschließlich aller Snapshot-Felder;
- die isolierte, newest-first Kaufhistorie pro Identity;
- deterministische Reihenfolge bei identischen `purchased_at`-Zeitpunkten über `id DESC`;
- mehrseitige Keyset-Pagination ohne Duplikate und ohne ausgelassene Käufe;
- `NextCursor` auf der letzten Seite und die leere History;
- den echten API-Host mit Offer-Storefront, DTO-Abbildung, Purchase-Lookup und History-Cursor;
- den echten API-Host mit bezahlten und kostenlosen HTTP-Purchases, vollständigem Response,
  Location, Idempotenz, gezielter Fehlerabbildung und atomarem Producer-Outbox-Write;
- ungültige Route-IDs, Page Sizes und malformed, ungültige oder Identity-fremde API-Cursor;
- einen zwischen Seiten neu persistierten, zeitlich neueren Kauf ohne Rückwärtsbewegung
  des bereits ausgegebenen Cursors.
- den echten API-Host mit serverseitigem Management-Create, vollständiger interner
  Kataloglesesicht, allen gezielten Katalogmutationen einschließlich SortOrder, der
  autoritativen `sort_order ASC, id ASC`-Reihenfolge, No-op-Enable/Disable und gezielter
  ProblemDetails-Abbildung;
- die unveränderte Trennung der Management-Lesesicht von der Storefront, die terminale
  Archivierung sowie spätere Purchase-Wirkungen von Preis-, Availability- und Kauflimit-
  Mutationen.

Die Unit Tests prüfen zusätzlich den vollständigen Serialize-/Deserialize-Roundtrip von
`shop.purchase-completed` v1 über die bestehende explizite Messaging-Registry.

Die Worker-Integration prüft zusätzlich die Verarbeitung des bekannten Events durch den echten
Worker und den Notifications-Inbox-Eintrag. Bewusst aus diesem Shop-V1-Scope
ausgeschlossen bleiben Shop-eigene Admin-UI und Shop-eigene Authentication/Authorization;
die gemeinsame Administration stellt die geschützte Katalogansicht und Management-Grenze,
ein Shop-owned Event-Consumer, Warenkorb, variable Purchase-Menge, Stock, Kategorien,
zusätzliche Metadaten, Discounts, Coupons, Refunds, Purchase-Cancellation, Ledger,
Saga/Compensation, Distributed Transactions, globale Unit-of-Work-Abstraktionen, generische
Repositories, generische Pagination-Foundations, Inventory-Item-Instanzen, Titles-/Rewards-/
Achievement-Ausführung oder eine generische `OfferTarget`-Abstraktion. Ebenfalls nicht
erforderlich sind Drag & Drop, Bulk-Reorder, Delete, Soft Delete, Unarchive und Restore;
die Archivierung ist terminal. Authentication/Authorization erfolgt über Administration V1;
Worker, Consumer, Contracts und Event-Versionen werden für V1 nicht
erweitert.

## Abschlussaudit

Der Abschlussaudit des zusammenhängenden Shop-V1-Auftrags bestätigt folgende Zustände:

| Prüffläche | Ergebnis |
| --- | --- |
| Domain | Vollständig für den beschlossenen Scope: validiertes Angebot, Preis, Availability, Aktivierung, terminale Archivierung, SortOrder, Kauflimit, unveränderliches Ziel-Item und unveränderlicher Purchase-Snapshot. |
| Application | Vollständige Katalog-, Storefront-, Purchase- und History-Use-Cases; keine weitere belegte Shop-owned V1-Funktion fehlt. |
| Persistence | Vier unveränderliche Shop-Migrationen, ausschließlich Shop-eigene Tabellen, gezielte Indizes, Row-Locks, Guard-Semantik, atomarer Commit/Rollback und historische Integrität. Eine Schema-Evolution ist für den beschlossenen Scope nicht erforderlich. |
| API/Storefront | Öffentliche sichtbare Katalogliste, Einzelangebot, direkter Purchase, Purchase-Lookup und identity-isolierte Keyset-History mit API-eigenen DTOs und ProblemDetails. Interne Zustände bleiben in der Management-Antwort. |
| Management | Create, vollständige interne Katalogsicht, alle fachlichen Mutationen, Enable/Disable und terminale Archivierung sind HTTP-erreichbar. Die Grenze ist durch Administration-Policies, Anti-Forgery, Audit und Operations geschützt. |
| Messaging/Worker | `shop.purchase-completed` v1 bleibt unverändert, wird atomar über die vorhandene Outbox erzeugt und im Worker vom eigenständigen Notifications-Consumer verarbeitet. |
| Modulgrenzen | Shop referenziert nur erlaubte Contracts und technische Foundations; kein Cross-Module-SQL und keine fremde Modulimplementierung. |
| Tests | Domain-, Application-, PostgreSQL-, API-, Messaging-, Worker- und Architekturabdeckung für den vollständigen Scope ist vorhanden. |
| Dokumentation | README, API-, Modul-, Persistence-, Messaging- und diese Shop-Dokumentation beschreiben den realen V1-Zustand sowie die bewussten Ausschlüsse. |

Der Audit findet keine offene Shop-owned V1-Lücke, keinen verbliebenen Shop-TODO, keine neue
Cross-Module-Kopplung und keine durch den Abschlussauftrag entstandene technische Schuld.
