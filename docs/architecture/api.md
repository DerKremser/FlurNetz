# FlurNetz.Api

## Rolle des Hosts

`FlurNetz.Api` ist ein eigenständiger ausführbarer FlurNetz-Host und ausschließlich Composition
Root und HTTP-Adapter. Er konfiguriert den ASP.NET-Core-Host, liest die PostgreSQL-
Konfiguration, registriert die technische Persistence Foundation, bindet das Identity-Modul und
den read-only Shop ein, führt die Startmigrationen aus und ordnet HTTP-Endpunkte zu. Der unabhängige
`FlurNetz.Worker` ist der separate Runtime-Host für Messaging und wird vom API-Host weder
referenziert noch gestartet.

Domain-Regeln, Use-Case-Ablauf, SQL, Repositories und fachliche Transaktionen verbleiben in
den jeweils zuständigen Schichten. Der Host erzeugt keine `CommunityIdentity`, vergibt keine
GUID und greift nicht direkt auf ein Repository zu.

## Abhängigkeiten

Der API-Host referenziert für diesen Slice ausschließlich:

- `FlurNetz.Persistence` für Connection Factory, Transaktions- und Migration-Foundation
- `FlurNetz.Modules.Identity` für `AddIdentityModule()` und den bestehenden Identity-Slice
- `FlurNetz.Modules.Identity.Contracts` für die explizite HTTP-Grenze
- `FlurNetz.Modules.Shop` für `AddShopReadOnlyModule()` und die bestehenden Shop-Read-Use-Cases
- `FlurNetz.Modules.Shop.Contracts` für die Shop-Identifier

Die erlaubte Richtung lautet:

`FlurNetz.Api` → `FlurNetz.Modules.Identity`/`FlurNetz.Modules.Shop` → `FlurNetz.Persistence`

`FlurNetz.Persistence`, `FlurNetz.Messaging`, `FlurNetz.BuildingBlocks`, Contracts und
Fachmodule referenzieren die API nicht. Weitere Fachmodule werden zur Laufzeit nicht
registriert. Der API-Host referenziert weder Economy, Inventory, Messaging noch Worker;
Messaging ist deshalb kein Runtime-Bestandteil dieses Hosts und die Outbox-Verarbeitung läuft
ausschließlich im unabhängigen `FlurNetz.Worker`.

## PostgreSQL und Startup

Die Verbindung wird über `ConnectionStrings:FlurNetz` konfiguriert. Der Repository-Stand
enthält nur einen leeren Wert als sicheren Basiseintrag. Lokale Werte werden über User Secrets
oder die Umgebungsvariable `ConnectionStrings__FlurNetz` bereitgestellt.

Der Host verwendet die vorhandene `PostgreSqlConnectionFactory` und erzeugt keine zweite
`NpgsqlDataSource`-, Connection- oder Transaction-Infrastruktur. Vor dem Start des HTTP-
Listeners löst der Host den bestehenden `MigrationRunner` auf und führt die registrierten
Migrationsquellen aus. In diesem Host sind das die Identity- und Shop-Quelle; dadurch werden
`Identity:1:CreateCommunityIdentities`, `Shop:1:CreateShopOffers` und
`Shop:2:CreateShopPurchases` ausgeführt. Die technische
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

Der read-only Shop ist über folgende Endpunkte erreichbar:

```text
GET /api/shop/offers
GET /api/shop/offers/{offerId}
GET /api/shop/purchases/{purchaseId}
GET /api/shop/identities/{communityIdentityId}/purchases?pageSize={pageSize}&cursor={cursor}
```

Die Offer-Storefront liefert ausschließlich `IsEnabled == true` und zum einmal ermittelten
aktuellen Zeitpunkt verfügbare Angebote. Der Einzel-Lookup antwortet für unbekannte,
deaktivierte, zukünftige oder abgelaufene Offers mit `404 Not Found`. Purchases werden mit
ihrem historischen Snapshot als API-eigenes DTO geliefert.

Die History bleibt identity-isoliert, newest-first und verwendet
`purchased_at DESC, id DESC` ohne Offset, Total Count oder Cross-Page-Snapshot. `pageSize`
hat Default `50` und die Grenzen `1` bis `100`. Der HTTP-Cursor ist API-eigen, opaque und
enthält als UTF-8-JSON mit interner Version `1` die Identity, den Kaufzeitpunkt und die
Purchase-ID; anschließend wird er Base64Url-kodiert. Malformed, unvollständige,
unbekannt versionierte oder fremde Cursor liefern `400 Bad Request`.

Es gibt bewusst keinen HTTP-Purchase-Endpunkt. Der interne `PurchaseShopOffer` bleibt
vorhanden, wird durch diese API-Komposition aber nicht runtime-erreichbar gemacht, weil der
API-Host weiterhin ausschließlich `AddShopReadOnlyModule()` registriert und keinen Write-Pfad
zuordnet. Der separate Worker kennt `shop.purchase-completed` v1 inzwischen über
`FlurNetz.Modules.Shop.Contracts`, registriert aber bewusst keinen fachlichen Shop-Consumer;
dieses Contract-Wiring fügt dem API-Host keinen HTTP-Endpunkt hinzu. Ein HTTP-Purchase folgt
erst in einem separaten späteren Slice.

## Fehlerbehandlung und aktueller Umfang

Der Host verwendet `AddProblemDetails()`, in Development die Developer Exception Page und
außerhalb von Development `UseExceptionHandler()`. Damit werden ungefangene technische
Fehler nicht als unkontrollierte Stacktrace-Antwort ausgeliefert.

Aktuell gibt es bewusst:

- keine Authentifizierung oder Autorisierung
- kein JWT, Cookie, OAuth oder Identity Framework
- kein Messaging Runtime Processing, keine Outbox-Loop und keinen Worker im API-Prozess
- keine Twitch-, Streamer.bot-, Discord-, YouTube- oder Kick-Integration
- keine weiteren fachlichen Module außer Identity und dem read-only Shop im API-Host

## Tests

`FlurNetz.Api.IntegrationTests` verwendet `WebApplicationFactory` für den echten API-Host und
Testcontainers PostgreSQL. Die Testkonfiguration überschreibt den Connection String über den
ASP.NET-Core-Testhost; Produktionskonfiguration und Secrets werden nicht verändert.

Die Tests prüfen:

- Startup gegen eine leere PostgreSQL-Datenbank inklusive Identity- und Shop-Migrationen
- `POST /api/identities` mit `201 Created` und gültiger ID
- Übereinstimmung zwischen Response-ID und `community_identities`
- mehrere Requests mit unterschiedlichen IDs und persistierten Datensätzen
- read-only Offer-Storefront mit Enabled-/Availability-Filter und vollständiger DTO-Abbildung
- Offer- und Purchase-Lookups mit `200` beziehungsweise `404`
- identity-isolierte, newest-first History mit `pageSize`, Keyset-Cursor und letzter Seite
- leere beziehungsweise unbekannte Identity-History sowie ungültige IDs, Page Sizes und Cursor
- fehlenden HTTP-Purchase-Endpunkt
- Startup-Abbruch bei nicht erreichbarer PostgreSQL-Datenbank

Die Architekturtests prüfen zusätzlich die erlaubten Host-Referenzen, die verbotene Richtung
`*` → `FlurNetz.Api` und dass der API-Host keine Repository-, Domain- oder Migrationstypen
enthält.
