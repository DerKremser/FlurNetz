# Shop-Katalog-Slice

## Verantwortung

`FlurNetz.Modules.Shop` besitzt das fachliche Angebotsmodell und den internen, persistierten
Angebotskatalog. Dieser Slice kennt Angebotsstammdaten, Preise, Verfügbarkeit, Kauflimits und
Aktivierung. Er kennt weder Käufe noch Identitäten, Economy, Bestandsvergabe oder externe
Schnittstellen.

## Öffentliche Contracts und Domain

`FlurNetz.Modules.Shop.Contracts` enthält ausschließlich den unveränderlichen
`ShopOfferId`. `ItemDefinitionId` wird nicht dupliziert, sondern aus
`FlurNetz.Modules.Inventory.Contracts` verwendet. `ShopPurchaseId`,
`ShopPurchaseRequestId`, Kauf-Commands, Queries und Events sind nicht vorhanden.

`ShopOffer` enthält `ShopOfferId`, `ItemDefinitionId`, den kanonisch getrimmten
`DisplayName` (1–200 Unicode-Skalarwerte), die optionale nicht-leere `Description` (höchstens
2000 Unicode-Skalarwerte), `ShopPrice`, `IsEnabled`, das halboffene `AvailabilityWindow` und
das optionale positive `PurchaseLimitPerIdentity` als `int?`. U+0000 und nicht wohlgeformtes
UTF-16 werden bereits an der Domain-Grenze abgewiesen. Neue Angebote sind immer deaktiviert.

Preis und Kauflimit besitzen die Invarianten `price >= 0` beziehungsweise `null oder > 0`.
Beim Availability-Fenster gilt `[AvailableFrom, AvailableUntil)`: Der Beginn ist inklusive,
das Ende exklusiv; sind beide Grenzen gesetzt, muss der Beginn vor dem Ende liegen. Gesetzte
Grenzen werden als absolute UTC-Instants kanonisiert und müssen exakt auf einer Mikrosekunden-
Grenze liegen, damit jeder akzeptierte Wert verlustfrei in PostgreSQL `timestamptz` persistiert
werden kann. Sub-Mikrosekundenwerte werden weder abgerundet noch aufgerundet; ein nach der
UTC-Kanonisierung leeres Fenster ist ungültig.
Angebots-ID und Item-Definition-ID bleiben unveränderlich. DisplayName, Description, Preis,
Availability, Kauflimit und Aktivierung werden ausschließlich über gezielte Domainmethoden
geändert.

`ShopOffer.Rehydrate(...)` ist der kontrollierte Persistence-Einstieg der Domain. Er stellt
alle persistierten Werte einschließlich des Aktivierungszustands wieder her und validiert
dieselben Invarianten wie `Create`. Er führt keine öffentlichen Setter ein und enthält keine
Persistence-Typen.

## Interne Application-Grenze

`IShopOfferStore` liegt im `FlurNetz.Modules.Shop`-Projekt und nicht in `Shop.Contracts`. Die
Grenze bietet ausschließlich `AddAsync`, `GetAsync`, `ListAsync` und
`Task<bool> ExecuteAsync(ShopOfferId, Func<ShopOffer, bool>, CancellationToken)`. Der
nicht-generische boolesche Callback kann technisch keine `Task`- oder `Task<T>`-Rückgabe
annehmen und bleibt damit synchron auf Domainlogik beschränkt.

Die internen Katalog-Use-Cases sind:

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

Create erzeugt die `ShopOfferId` serverseitig, validiert das Angebot über die Domain, schreibt
es deaktiviert und liefert das persistierte Domainobjekt zurück. Unbekannte IDs liefern beim
Lesen `null`; eine unbekannte ID bei einer Mutation führt zu
`ShopOfferNotFoundException`. Keine dieser Fehler- oder Use-Case-Klassen wird in
`Shop.Contracts` exportiert.

## PostgreSQL-Persistenz

Die Migration `Shop:1:CreateShopOffers` wird von `ShopMigrationSource` geliefert und besitzt
ausschließlich die Tabelle `shop_offers`:

| Spalte | PostgreSQL-Typ | Nullbarkeit |
| --- | --- | --- |
| `id` | `uuid` | `NOT NULL` |
| `item_definition_id` | `uuid` | `NOT NULL` |
| `display_name` | `varchar(200)` | `NOT NULL` |
| `description` | `varchar(2000)` | `NULL` |
| `price` | `bigint` | `NOT NULL` |
| `is_enabled` | `boolean` | `NOT NULL` |
| `available_from` | `timestamptz` | `NULL` |
| `available_until` | `timestamptz` | `NULL` |
| `purchase_limit_per_identity` | `integer` | `NULL` |

Der Primärschlüssel besteht ausschließlich aus `id`. Datenbank-Checks sichern nicht-leere und
kanonisch getrimmte Texte, den nicht-negativen Preis, das positive Kauflimit und die gültige
Reihenfolge der Availability-Grenzen. Es gibt keine SQL-Defaults für fachliche Zustände,
insbesondere wird `is_enabled` beim Insert explizit geschrieben. Es gibt keine Foreign Keys
auf Inventory, Identity oder andere Cross-Module-Tabellen und keine weiteren Shop-Tabellen.

`ShopOfferStore` verwendet die bestehende PostgreSQL-/Dapper-Foundation. Add schreibt alle
Angebotsfelder ohne eigene fachliche Normalisierung. Get und List rehydrieren über den
Domainpfad; List verwendet `ORDER BY id`. Die Domain liefert für `timestamptz` bereits
kanonische UTC-Instants mit PostgreSQL-kompatibler Mikrosekundenpräzision; der Store dupliziert
diese Zeitlogik nicht und schreibt die Werte unverändert.

Mutation öffnet eine `PostgreSqlTransaction`, lädt genau die Zielzeile mit `SELECT ... FOR
UPDATE`, rehydriert das Angebot, bildet Vorher-/Nachher-Snapshots und aktualisiert nur bei
tatsächlicher Änderung. Die Snapshots umfassen DisplayName, Description, Price, IsEnabled,
beide Availability-Grenzen und PurchaseLimitPerIdentity; ID und ItemDefinitionId werden nie
überschrieben. Ein Domain-No-op liefert `false` und führt zu keinem `UPDATE`. Danach wird
committed, bei jedem Fehler vollständig zurückgerollt. Das serialisiert konkurrierende
Änderungen desselben Angebots und lässt unterschiedliche Angebote unabhängig mutieren.

## Modulregistrierung und Abhängigkeiten

`ShopModule.AddShopModule(...)` registriert ausschließlich `IShopOfferStore` mit
`ShopOfferStore`, die zehn aktuellen Katalog-Use-Cases sowie `ShopMigrationSource` als
`IMigrationSource`. Es registriert keine globale Clock, Messaging-Dienste, Host-, API-,
Purchase- oder Administration-Komponenten. Die Host-Verdrahtung bleibt außerhalb des Moduls.

`FlurNetz.Modules.Shop` referenziert ausschließlich `Shop.Contracts`, `Inventory.Contracts`
und `FlurNetz.Persistence`. `Shop.Contracts` bleibt frei von FlurNetz-Referenzen. Identity,
Economy, Messaging, Rewards, Titles, Achievements, Administration, API, Worker und fremde
Modulimplementierungen bleiben ausgeschlossen.

## Tests und bewusste Nicht-Ziele

`FlurNetz.Modules.Shop.Tests` prüft Rehydration, Invarianten, unveränderliche Ziel-IDs und
die Delegation der Katalog-Use-Cases. `FlurNetz.Modules.Shop.IntegrationTests` verwendet
echtes PostgreSQL über Testcontainers oder `FLURNETZ_TEST_CONNECTION_STRING` und prüft
Migration/Idempotenz/Checksum, den vollständigen `shop_`-Relationsumfang ohne benutzerdefinierte
Trigger, das exakte Schema ohne Foreign Keys oder Defaults, alle fachlichen DB-Constraints,
Unicode- und Zeitpräzisions-Roundtrips, alle Katalogmutationen, DB-Rollback und DB-seitige
No-op-Beobachtung sowie deterministische Row-Lock-Nebenläufigkeit für gleiche und
unterschiedliche Offers. `ShopArchitectureTests` sichert Reference Graph, Typgrenzen,
Migrationseigentum, DI-Scope, den synchronen Store-Callback und den Ausschluss vorzeitiger
Integration.

Nicht enthalten sind `ShopPurchase`, `ShopPurchaseId`, `ShopPurchaseRequestId`,
`PurchaseShopOffer`, `shop_purchases`, Purchase Guards/Requests, Economy, Coins, Balance,
Inventory Grant, transaction-aware Inventory-Capability, Identity, Messaging, Domain Events,
Integration Events, Outbox, Inbox, API, Worker-Anbindung, Administration, globales
`ShopEnabled`, Kategorien, SortOrder, Featured, Assets, Bilder, Rabatte, Stock, Warenkorb,
Refunds, Titles-/Rewards-Ziele, eine generische `OfferTarget`-Abstraktion und Seed-Daten.
