# FlurNetz.Api

## Rolle des Hosts

`FlurNetz.Api` ist ein eigenständiger ausführbarer FlurNetz-Host und ausschließlich Composition
Root und HTTP-Adapter. Er konfiguriert den ASP.NET-Core-Host, liest die PostgreSQL-
Konfiguration, registriert die technische Persistence Foundation, bindet das Identity-Modul,
den vollständigen Shop-Purchase-Slice sowie die schmalen Economy-/Inventory-Capabilities ein,
führt die Startmigrationen aus und ordnet Storefront-, Purchase- und Shop-Management-
HTTP-Endpunkte zu. Der unabhängige
`FlurNetz.Worker` ist der separate Runtime-Host für Messaging und wird vom API-Host weder
referenziert noch gestartet.

Domain-Regeln, Use-Case-Ablauf, SQL, Repositories und fachliche Transaktionen verbleiben in
den jeweils zuständigen Schichten. Der Host erzeugt keine `CommunityIdentity`, vergibt keine
GUID und greift nicht direkt auf ein Repository zu.

## Abhängigkeiten

Der API-Host referenziert für diesen Slice:

- `FlurNetz.Persistence` für Connection Factory, Transaktions- und Migration-Foundation
- `FlurNetz.Messaging` für die explizite Producer-Registry, Serialisierung, Outbox und Migration
- `FlurNetz.Modules.Economy` für `AddEconomyDebitCapability()`
- `FlurNetz.Modules.Identity` für `AddIdentityModule()` und den bestehenden Identity-Slice
- `FlurNetz.Modules.Identity.Contracts` für die explizite HTTP-Grenze
- `FlurNetz.Modules.Inventory` für `AddInventoryGrantCapability()`
- `FlurNetz.Modules.Inventory.Contracts` für die fachliche `ItemDefinitionId`-Abbildung beim
  Management-Create
- `FlurNetz.Modules.Shop` für `AddShopModule()` und die bestehenden Shop-Use-Cases
- `FlurNetz.Modules.Shop.Contracts` für die Shop-Identifier

Die erlaubte Richtung lautet:

`FlurNetz.Api` → Module/Contracts/technische Foundations → `FlurNetz.Persistence`

`FlurNetz.Persistence`, `FlurNetz.Messaging`, `FlurNetz.BuildingBlocks`, Contracts und
Fachmodule referenzieren die API nicht. Weitere Fachmodule werden zur Laufzeit nicht
registriert. Der API-Host referenziert weiterhin keinen Worker. Messaging ist im API-Prozess
nur als Producer-Runtime vorhanden; Outbox-Processing und Consumer-Laufzeit bleiben beim
unabhängigen `FlurNetz.Worker`.

## PostgreSQL und Startup

Die Verbindung wird über `ConnectionStrings:FlurNetz` konfiguriert. Der Repository-Stand
enthält nur einen leeren Wert als sicheren Basiseintrag. Lokale Werte werden über User Secrets
oder die Umgebungsvariable `ConnectionStrings__FlurNetz` bereitgestellt.

Der Host verwendet die vorhandene `PostgreSqlConnectionFactory` und erzeugt keine zweite
`NpgsqlDataSource`-, Connection- oder Transaction-Infrastruktur. Vor dem Start des HTTP-
Listeners löst der Host den bestehenden `MigrationRunner` auf und führt die registrierten
Migrationsquellen aus. In diesem Host sind die Identity-, Economy-, Inventory-, Shop- und
Messaging-Quellen registriert; dadurch werden genau `Identity:1:CreateCommunityIdentities`,
`Economy:1:CreateCommunityEconomies`, `Inventory:1:CreateCommunityInventoryEntries`,
`Shop:1:CreateShopOffers`, `Shop:2:CreateShopPurchases`, `Shop:3:AddShopOfferSortOrder`,
`Shop:4:AddShopOfferArchiveState` und
`Messaging:1:CreateOutboxAndInbox` ausgeführt. Die technische
`flurnetz_persistence.migration_history` wird vom Runner selbst verwaltet. Schlägt die
Verbindung oder eine Migration fehl, wird der Fehler mit ASP.NET-Core-Logging auf Critical-
Ebene geloggt und der Startup abgebrochen.

## HTTP-Endpunkte

Der erste HTTP-Endpunkt ist:

```text
POST /api/identities
```

Der vorhandene `CreateCommunityIdentity`-Use-Case benötigt keine Eingabedaten. Deshalb wird
kein künstlicher Request-DTO verwendet; ein leerer POST-Body ist der gültige Request-Vertrag.
Der HTTP-CancellationToken wird an `ExecuteAsync` weitergereicht.

Bei Erfolg liefert der Adapter `201 Created` mit einem API-spezifischen DTO:

```json
{
  "id": "<erzeugte-guid>"
}
```

Die Domain-Entity und der Contract-Value-Type werden nicht als HTTP-Vertrag serialisiert.
Das DTO mappt `CommunityIdentityId.Value` explizit auf `id`. Da es noch keinen öffentlichen
GET-Endpunkt gibt, wird keine künstliche `CreatedAtRoute`-Location erzeugt.

Die Shop-Storefront, der Purchase und die read-only Purchase-History sind über folgende
Endpunkte erreichbar:

```text
GET /api/shop/offers
GET /api/shop/offers/{offerId}
POST /api/shop/offers/{offerId}/purchases
GET /api/shop/purchases/{purchaseId}
GET /api/shop/identities/{communityIdentityId}/purchases?pageSize={pageSize}&cursor={cursor}
```

Die Offer-Storefront liefert ausschließlich nach der Regel
`IsEnabled && !IsArchived && IsAvailableAt(now)` und zum einmal ermittelten
aktuellen Zeitpunkt verfügbare Angebote und behält dabei die fachliche Reihenfolge
`sort_order ASC, id ASC` des Stores. Der Einzel-Lookup antwortet für unbekannte,
deaktivierte, archivierte, zukünftige oder abgelaufene Offers mit `404 Not Found`. Purchases werden mit
ihrem historischen Snapshot als API-eigenes DTO geliefert.

Die History bleibt identity-isoliert, newest-first und verwendet
`purchased_at DESC, id DESC` ohne Offset, Total Count oder Cross-Page-Snapshot. `pageSize`
hat Default `50` und die Grenzen `1` bis `100`. Der HTTP-Cursor ist API-eigen, opaque und
enthält als UTF-8-JSON mit interner Version `1` die Identity, den Kaufzeitpunkt und die
Purchase-ID; anschließend wird er Base64Url-kodiert. Malformed, unvollständige,
unbekannt versionierte oder fremde Cursor liefern `400 Bad Request`.

Der Purchase ist über folgenden zusätzlichen Endpunkt erreichbar:

```text
POST /api/shop/offers/{offerId}/purchases
```

Der API-eigene Request-Vertrag enthält ausschließlich zwei GUIDs:

```json
{
  "requestId": "<request-guid>",
  "communityIdentityId": "<community-identity-guid>"
}
```

`offerId` bleibt Route-Parameter. Der Adapter validiert alle drei nicht-leeren GUIDs, bildet
`ShopOfferId`, `ShopPurchaseRequestId` und `CommunityIdentityId` und ruft ausschließlich
`PurchaseShopOffer.ExecuteAsync(...)` auf. Bei Erfolg liefert er den vollständigen bestehenden
`ShopPurchaseResponse` mit `201 Created` und Location
`/api/shop/purchases/{purchaseId}`. Ein identischer Request wird idempotent mit derselben
Purchase-ID und Location wiedergegeben; es gibt kein Replay-Flag.

Die bekannte Fehlerabbildung lautet: ungültige Route-/Request-Identifier `400 Bad Request`,
unbekanntes Offer oder unbekannte Identity `404 Not Found`, nicht kaufbares Offer, Kauflimit,
Idempotenzkonflikt und unzureichender Economy-Saldo `409 Conflict`. Diese Antworten sind
ProblemDetails. Unerwartete Fehler, insbesondere sonstige `InvalidOperationException`-Fälle,
bleiben `500 Internal Server Error`.

Der separate Worker kennt `shop.purchase-completed` v1 weiterhin über
`FlurNetz.Modules.Shop.Contracts`, registriert aber bewusst keinen fachlichen Shop-Consumer.
Der API-Host ist nur Producer: Er kann die Outbox beschreiben, startet aber keinen
`OutboxProcessor`, keinen Messaging-Worker und keinen Inbox-Consumer.

### Shop-Katalogverwaltung

Die getrennte Management-Grenze für den internen Angebotskatalog lautet:

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

Create bildet `itemDefinitionId`, `displayName`, die optionale `description`, `price`,
optionale Availability-Grenzen, das optionale `purchaseLimitPerIdentity` und den optionalen
`sortOrder` ab. Fehlt `sortOrder`, wird `0` verwendet; nur Werte `>= 0` sind gültig. Die
Offer-ID wird durch `CreateShopOffer` serverseitig erzeugt; neue Angebote bleiben deaktiviert.
Für Leseantworten verwendet die Management-Grenze ein eigenes API-DTO einschließlich
`IsEnabled`, `IsArchived` und `sortOrder`. Sie ruft `GetShopOffer` und `ListShopOffers` auf und sieht daher
auch deaktivierte, archivierte, zukünftige und abgelaufene Angebote. Die Management-Liste folgt
`sort_order ASC, id ASC`. Die Mutationsrouten rufen jeweils genau den passenden vorhandenen
Use-Case auf und antworten bei Erfolg mit `204 No Content`.

Die SortOrder-Mutation verwendet den API-eigenen Request `ChangeShopOfferSortOrderRequest`.
`PUT /api/admin/shop/offers/{offerId}/sort-order` liefert für gültige Änderungen und fachliche
No-ops `204 No Content`, für unbekannte gültige IDs `404 ProblemDetails` und für ungültige
Route-IDs, fehlende oder malformed Bodies sowie negative Werte `400 ProblemDetails`.

`POST /api/admin/shop/offers/{offerId}/archive` besitzt keinen Request-Body und liefert bei
erstmaliger wie wiederholter Archivierung `204 No Content`. Unbekannte gültige IDs liefern
`404 ProblemDetails`, ungültige IDs `400 ProblemDetails`; Enable eines archivierten Angebots
liefert `409 Conflict` als ProblemDetails.

Der Adapter greift weder auf `IShopOfferStore` noch auf Dapper, Npgsql oder Tabellen zu und
öffnet keine eigene Transaktion. Unbekannte gültige Offer-IDs liefern `404 Not Found`,
ungültige IDs, malformed JSON und bekannte fachliche Eingabefehler `400 Bad Request`; alle
bekannten Fehlerantworten sind `ProblemDetails`. Ein erneutes Enable oder Disable bleibt ein
fachlicher No-op und wird nicht als Conflict behandelt. Die Storefront-Routen bleiben
semantisch unverändert und zeigen weiterhin ausschließlich aktivierte und aktuell verfügbare
Offers.

Archivierung ist terminal und ausschließlich über die Management-Mutation möglich. SortOrder
ist ausschließlich implementation-owned beziehungsweise API-eigener Management-
Zustand. `FlurNetz.Modules.Shop.Contracts` wird nicht erweitert; der Purchase-Snapshot und
`shop.purchase-completed` v1 bleiben unverändert. Es gibt keinen neuen Consumer, keine
Worker-Änderung, keine Verschiebung in das Administration-Modul, kein Admin-Frontend und
keine Delete-, Unarchive- oder Restore-Route. Authentication/Authorization bleibt ein separater späterer
Security-/Host-Scope.

## Messaging-Producer-Runtime

Die API registriert explizit genau `ShopPurchaseCompletedIntegrationEvent` mit
`MessageType` und `SchemaVersion` aus dem Contract. Dazu kommen
`IntegrationEventTypeRegistry`, `IntegrationEventJsonSerializer`,
`IIntegrationEventSerializer`, `IIntegrationEventPublisher` als
`PostgreSqlOutboxPublisher` und die bestehende `MessagingMigrationSource`. Die vom Shop-Modul
über `TryAddSingleton` bereitgestellte `IClock` wird wiederverwendet. Es gibt kein Assembly
Scanning und keine Registrierung anderer Eventtypen.

## Fehlerbehandlung und aktueller Umfang

Der Host verwendet `AddProblemDetails()`, in Development die Developer Exception Page und
außerhalb von Development `UseExceptionHandler()`. Damit werden ungefangene technische
Fehler nicht als unkontrollierte Stacktrace-Antwort ausgeliefert.

Aktuell gibt es bewusst:

- keine Authentifizierung oder Autorisierung
- kein JWT, Cookie, OAuth oder Identity Framework
- kein Messaging Runtime Processing, keine Outbox-Loop, kein Inbox-Consumer und keinen Worker im API-Prozess
- keine Twitch-, Streamer.bot-, Discord-, YouTube- oder Kick-Integration
- keine HTTP-Endpunkte für Economy oder Inventory
- kein Admin-Frontend und keine Authentication/Authorization für die Shop-Management-Grenze;
  vor externem Produktivbetrieb ist dafür ein separater Security-/Host-Slice erforderlich
- keinen Cart-, Stock-, Discount-, Coupon-, Refund- oder Cancellation-Flow

## Tests

`FlurNetz.Api.IntegrationTests` verwendet `WebApplicationFactory` für den echten API-Host und
Testcontainers PostgreSQL. Die Testkonfiguration überschreibt den Connection String über den
ASP.NET-Core-Testhost; Produktionskonfiguration und Secrets werden nicht verändert.

Die Tests prüfen:

- Startup gegen eine leere PostgreSQL-Datenbank inklusive aller acht registrierten Identity-,
  Economy-, Inventory-, Shop- und Messaging-Migrationen
- `POST /api/identities` mit `201 Created` und gültiger ID
- Übereinstimmung zwischen Response-ID und `community_identities`
- mehrere Requests mit unterschiedlichen IDs und persistierten Datensätzen
- read-only Offer-Storefront mit Enabled-/Availability-Filter und vollständiger DTO-Abbildung
- Offer- und Purchase-Lookups mit `200` beziehungsweise `404`
- identity-isolierte, newest-first History mit `pageSize`, Keyset-Cursor und letzter Seite
- leere beziehungsweise unbekannte Identity-History sowie ungültige IDs, Page Sizes und Cursor
- bezahlte und kostenlose HTTP-Purchases einschließlich vollständigem Snapshot, Location,
  Economy-Debit, Inventory-Grant, Purchase-, Request- und Outbox-Count
- idempotenten Replay und Idempotency-Conflict ohne zweite Business-Wirkung
- unbekannte oder nicht kaufbare Offers, unbekannte Identity, Kauflimit und unzureichenden Saldo
  mit gezieltem Status und vollständigem Rollback
- ungültige Route-/Request-Identifier und malformed JSON mit `400 Bad Request`
- die getrennte Shop-Management-Grenze mit Create, vollständiger Katalogsicht, allen gezielten
  Mutationen einschließlich SortOrder, autoritativer Katalogreihenfolge, Enable/Disable,
  No-op-Stabilität, 404/400-ProblemDetails und serverseitiger ID
- unveränderte Storefront-/Purchase-Semantik nach Management-Mutationen einschließlich
  Preis-Snapshot, Availability und Kauflimit
- Producer-only-Verhalten: pending Outbox, kein API-Processing und kein Inbox-Eintrag
- Startup-Abbruch bei nicht erreichbarer PostgreSQL-Datenbank

Die Architekturtests prüfen zusätzlich die erlaubten Host-Referenzen, die verbotene Richtung
`*` → `FlurNetz.Api` und dass der API-Host keine Repository-, Domain- oder Migrationstypen
enthält.
