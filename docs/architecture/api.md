# FlurNetz.Api

## Rolle des Hosts

Für Automation V1 ist die API zusätzlich die reine Management-Grenze unter
/api/admin/automation/rules. Sie registriert AddAutomationModule() und mappt Create, Get, List,
Replace, Enable, Disable, Archive sowie die Execution-History. Sie registriert keine
Automation-Consumer, keinen OutboxProcessor und führt keine Automation-Action aus.

Für Overlay V1 registriert die API zusätzlich `AddOverlayModule()` und mappt die interne
Channel-Management-Grenze unter `/api/admin/overlay/channels`, die transparente Browser Source
unter `/overlay/{sourceKey}` und den SSE-Stream unter
`/api/overlay/sources/{sourceKey}/stream`. Die API führt auch damit keine Automation Rules
und keinen OutboxProcessor aus.

`FlurNetz.Api` ist ein eigenständiger ausführbarer FlurNetz-Host und ausschließlich Composition
Root und HTTP-Adapter. Er konfiguriert den ASP.NET-Core-Host, liest die PostgreSQL-
Konfiguration, registriert die technische Persistence Foundation, bindet die Administration
und die dafür benötigten Owner-Reads/-Capabilities, den vollständigen Shop-V1-Purchase, die
persönliche Notifications-Inbox sowie die bestehenden Management-Grenzen ein, führt die
Startmigrationen aus und ordnet Storefront-, Purchase-, Notifications-, Admin-API- und
Razor-HTTP-Endpunkte zu. Der unabhängige
`FlurNetz.Worker` ist der separate Runtime-Host für Messaging und wird vom API-Host weder
referenziert noch gestartet.

Domain-Regeln, Use-Case-Ablauf, SQL, Repositories und fachliche Transaktionen verbleiben in
den jeweils zuständigen Schichten. Der Host erzeugt keine `CommunityIdentity`, vergibt keine
GUID und greift nicht direkt auf ein Repository zu.

## Abhängigkeiten

Der API-Host referenziert für die Shop-V1-Komposition:

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
- `FlurNetz.Modules.Notifications` für `AddNotificationsModule()` und die Inbox-Use-Cases
- `FlurNetz.Modules.Overlay` für `AddOverlayModule()`, Overlay-Use-Cases und die Browser-/SSE-
  Adaptergrenze
- `FlurNetz.Modules.Overlay.Contracts` für die Overlay-Alert-Contract-Werte
- `FlurNetz.Modules.Administration` und `FlurNetz.Modules.Administration.Contracts` für
  Credentials, Policies, Audit, Operations und die Admin-Composition
- `FlurNetz.Modules.Progression`, `FlurNetz.Modules.Rewards`, `FlurNetz.Modules.Titles`,
  `FlurNetz.Modules.Achievements`, `FlurNetz.Modules.Automation` und
  `FlurNetz.Modules.Integrations` für ihre expliziten Owner-Reads und Management-Use-Cases

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
Migrationsquellen aus. In diesem Host sind die Identity-, Economy-, Inventory-, Shop-,
Notifications-, Overlay-, Integrations- und Messaging-Quellen registriert; dadurch werden genau `Identity:1:CreateCommunityIdentities`,
`Economy:1:CreateCommunityEconomies`, `Inventory:1:CreateCommunityInventoryEntries`,
`Shop:1:CreateShopOffers`, `Shop:2:CreateShopPurchases`, `Shop:3:AddShopOfferSortOrder`,
`Shop:4:AddShopOfferArchiveState`, `Notifications:1:CreateCommunityNotifications`,
`Integrations:1:CreateExternalIdentityMappings`, `Messaging:1:CreateOutboxAndInbox` und
`Overlay:1:CreateOverlayChannelsAndAlerts` ausgeführt. Die technische
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

Die persönliche Notifications-Inbox ist über folgende API-eigene DTO-Grenze erreichbar:

```text
GET  /api/identities/{communityIdentityId}/notifications?pageSize={pageSize}&cursor={cursor}&unreadOnly={bool}
GET  /api/identities/{communityIdentityId}/notifications/unread-count
GET  /api/identities/{communityIdentityId}/notifications/{notificationId}
POST /api/identities/{communityIdentityId}/notifications/{notificationId}/read
POST /api/identities/{communityIdentityId}/notifications/{notificationId}/unread
POST /api/identities/{communityIdentityId}/notifications/read-all
```

Die Liste verwendet `created_at_utc DESC, id DESC`, `pageSize` 1 bis 100 und einen an Identity
und `unreadOnly` gebundenen API-Cursor. Einzel- und Mutationszugriffe sind identity-isoliert;
malformed IDs, Cursor, Filter oder Page Sizes liefern `400`, unbekannte oder fremde Notifications
`404`. Es gibt keinen öffentlichen Create-Endpunkt; die öffentliche Notifications-API bleibt
außerhalb des lokalen Admin-Cookie-Schemes.

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
`FlurNetz.Modules.Shop.Contracts` und registriert den Notifications-Consumer
`notifications.shop-purchase`.
Der API-Host ist nur Producer: Er kann die Outbox beschreiben, startet aber keinen
`OutboxProcessor`, keinen Messaging-Worker und keinen Inbox-Consumer.

### Administration und Shop-Katalogverwaltung

Die Webadministration ist unter `/admin` erreichbar. Login und Logout verwenden
`GET/POST /admin/login` beziehungsweise `POST /admin/logout`; das Passwortformular liegt
unter `/admin/account`. Der anonyme First-Run-Flow liegt unter `GET/POST /admin/setup` und
ist nur verfügbar, solange noch kein Administrator eingerichtet wurde. Er verlangt E-Mail,
Passwort, Bestätigung und das ausschließlich aus `Administration:Setup:Secret` gelesene
Setup-Gate. Nach erfolgreichem Setup ist die Route geschlossen. Beide Passwortseiten bieten
einen optionalen clientseitigen 24-Zeichen-Generator auf Basis von
`window.crypto.getRandomValues()`, ohne Browser-Persistenz oder serverseitigen
Generator-Endpunkt. Die sichtbaren Razor-Seiten umfassen Dashboard, Identity-Liste und
-Detail, Shop, Catalog, Automation, Integrations, Overlay, Audit, Account und den
einmaligen Setup-Flow.

Administration UI V1.1 ist serverseitig gerendert und nutzt die gemeinsame Admin-Shell mit
Design-Tokens, responsiver Navigation, Mobile-Drawer, Skip-Link, `aria-current`, sichtbarem
Keyboard-Fokus und `prefers-reduced-motion`. Die Accessibility-Baseline ist an WCAG 2.2 AA
orientiert, ohne eine formale Konformitätszertifizierung zu behaupten. Native Ressourcen
unterstützen Deutsch als Default/Fallback und Englisch als zweite Sprache. Die Auswahl wird
unter `/admin/account` pro Administrator persistiert (`preferred_culture` mit `NULL`, `de` oder
`en`) und beim nächsten Login erneut angewendet; sie ist keine globale UI-Einstellung. Die
Razor-UI lokalisiert auch bekannte Audit-Aktionen und Ressourcentypen und fällt bei unbekannten
technischen Werten sicher auf den Ausgangswert zurück.

Die permission-geschützten Admin-API-Reads und -Mutationen liegen unter:

```text
GET/POST /api/admin/identities
GET      /api/admin/identities/{communityIdentityId}
GET/POST /api/admin/economy/...
GET/POST /api/admin/progression/...
GET/POST /api/admin/inventory/...
GET/POST /api/admin/achievements/...
GET/POST /api/admin/titles/...
GET/POST /api/admin/rewards/...
GET      /api/admin/audit
```

Jede Policy verlangt `Administration.Access` und die konkrete Capability-Permission. Cookie-
authentifizierte Mutationen benötigen Anti-Forgery; High-Risk-Mutationen zusätzlich Reason,
RequestId, Audit und den idempotenten AdminOperation-Flow. `/api/admin` antwortet bei fehlender
Session mit `401` statt mit einem Login-HTML-Redirect und bei fehlender Berechtigung mit
`403`.

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
`shop.purchase-completed` v1 bleiben unverändert. Die Notifications-Inbox ist ein separates
Modul; es gibt keine Verschiebung in das Administration-Modul und keine Delete-, Unarchive-
oder Restore-Route. Die bestehende Admin-Razor-UI bleibt ein hostseitiger Management-Adapter;
Authentication/Authorization für die
Management-Grenze erfolgt über Administration V1; öffentliche Shop-Routen bleiben davon
getrennt.

## Messaging-Producer-Runtime

Die API registriert explizit genau `ShopPurchaseCompletedIntegrationEvent` mit
`MessageType` und `SchemaVersion` aus dem Contract. Dazu kommen
`IntegrationEventTypeRegistry`, `IntegrationEventJsonSerializer`,
`IIntegrationEventSerializer`, `IIntegrationEventPublisher` als
`PostgreSqlOutboxPublisher` und die bestehende `MessagingMigrationSource`. Die vom Shop-Modul
über `TryAddSingleton` bereitgestellte `IClock` wird wiederverwendet. Es gibt kein Assembly
Scanning und keine Registrierung anderer Eventtypen.

## Integrations-Management

Integrations V1 verwendet die interne Management-Grenze:

    POST   /api/admin/integrations/external-identities
    GET    /api/admin/integrations/external-identities/{provider}/{externalUserId}
    GET    /api/admin/integrations/external-identities/community/{communityIdentityId}
    DELETE /api/admin/integrations/external-identities/{provider}/{externalUserId}

Der POST verknüpft eine bereits vorhandene CommunityIdentityId mit einem validierten
Provider-Key und einer opaque externen User-ID. Ein identischer Link ist idempotent;
eine Verknüpfung derselben externen Identität mit einer anderen Community-Identität
liefert 409. Eine unbekannte Zielidentität liefert 404. GET und DELETE liefern für
unbekannte Mappings 404, ungültige Eingaben werden mit 400 und ProblemDetails
beantwortet. Die Response-DTOs gehören ausschließlich zur API und enthalten nur
Provider, externe User-ID und CommunityIdentityId.

Die Endpunkte sind über `Integrations.Read` beziehungsweise `Integrations.ManageMappings`,
Anti-Forgery und den gemeinsamen Admin-Ausführungskontext geschützt. Die API führt weiterhin
keine Twitch-Verbindung und keine automatische Identity-Erstellung aus.

## Fehlerbehandlung und Shop-V1-Umfang

Der Host verwendet `AddProblemDetails()`, in Development die Developer Exception Page und
außerhalb von Development `UseExceptionHandler()`. Damit werden ungefangene technische
Fehler nicht als unkontrollierte Stacktrace-Antwort ausgeliefert.

Für den API-Host und den beschlossenen Shop-V1-Umfang gibt es bewusst:

- keine allgemeine Community-Authentifizierung; die Administration verwendet ausschließlich
  ihr getrenntes lokales Cookie-Scheme
- kein JWT und keine OAuth-/Identity-Framework-Anbindung für die Administration
- kein Messaging Runtime Processing, keine Outbox-Loop, kein Inbox-Consumer und keinen Worker im API-Prozess
- keine Twitch-, Streamer.bot-, Discord-, YouTube- oder Kick-Integration
- keine HTTP-Endpunkte für Economy oder Inventory
- keinen öffentlichen Forgot-Password-Flow; `/admin/setup` ist nur während des einmaligen,
  gate-geschützten First-Run-Setups öffentlich verfügbar und danach geschlossen. Setup-POSTs
  sind CSRF- und rate-limit-geschützt und antworten mit `no-store`; es gibt kein Remember Me
  und keinen Source-Key-Readback
- keinen variablen Mengenparameter; ein Purchase gewährt verbindlich genau eine Inventory-Einheit
- keinen Cart-, Stock-, Kategorien-, zusätzlichen Metadaten-, Discount-, Coupon-, Refund- oder
  Cancellation-Flow; die fachlichen Entscheidungen sind in [shop.md](shop.md) festgehalten

## Tests

`FlurNetz.Api.IntegrationTests` verwendet `WebApplicationFactory` für den echten API-Host und
Testcontainers PostgreSQL. Die Testkonfiguration überschreibt den Connection String über den
ASP.NET-Core-Testhost; Produktionskonfiguration und Secrets werden nicht verändert.

Die Tests prüfen:

- Startup gegen eine leere PostgreSQL-Datenbank inklusive der registrierten Identity-,
  Administration-, Economy-, Progression-, Inventory-, Shop-, Notifications-, Automation-,
  Integrations-, Overlay- und Messaging-Migrationen
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
- Admin-Login, generische Loginfehler, Login-/Logout-CSRF, Rate-Limit, Session-Revocation und
  keine HTML-Redirects für `/api/admin`
- First-Run-Setup mit E-Mail-Login, Gate-Fehlern, Einmaligkeit, `no-store`, CSRF und ohne
  Passwort-/Setup-Geheimnis in Persistenz, Audit oder Operations
- geschützte Regressionen für Shop, Automation, Integrations und Overlay einschließlich
  High-Risk-Reason, RequestId, Audit und One-Time-Source-Key-Verhalten
- Admin-Shell, Static Assets, ARIA-/responsive Navigationssemantik, Skip-Link und
  Reduced-Motion-Styles
- symmetrische DE-/EN-Ressourcen, persistierte individuelle Administrator-Sprache,
  lokalisierte Auditdarstellung und Identity-Detailnavigation
- Notifications-Inbox mit vollständiger DTO-Abbildung, Einzel-Lookup, Cursor-/Filterbindung,
  Identity-Isolation, Unread Count und Read-/Unread-/Read-All-Mutationen
- Startup-Abbruch bei nicht erreichbarer PostgreSQL-Datenbank

Die Architekturtests prüfen zusätzlich die erlaubten Host-Referenzen, die verbotene Richtung
`*` → `FlurNetz.Api`, die Administration-Grenzen und dass der API-Host keine direkten SQL-
Zugriffe oder fremden Repository-/Migrationstypen enthält.
