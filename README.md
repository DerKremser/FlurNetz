# FlurNetz

FlurNetz ist ein modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst eine minimale BuildingBlocks-Grundlage, die technische Persistence Foundation, die Messaging Foundation, die physischen Grenzen der vorgesehenen Fachmodule, den ersten fachlichen Identity-Vertical-Slice, den ersten Engagement-Message-Recording-Slice mit Outbox, den ersten Progression-Inbox-Consumer, den ersten persistierten Economy-Vertical-Slice, den ersten persistierten und ausführbaren Rewards-Vertical-Slice, den ersten persistierten Inventory-Vertical-Slice, den ersten persistierten Titles-Vertical-Slice, den ersten persistierten Achievements-Vertical-Slice, den persistierten Shop-Angebotskatalog sowie unabhängige API- und Worker-Hosts. Der Cross-Module-Workflow ist Ende zu Ende gegen PostgreSQL getestet und kann durch den Worker kontinuierlich verarbeitet werden; eine Engagement-HTTP-Schnittstelle, eine Economy-API, Rewards-Runtime-Trigger, Titles-API, Achievement-Runtime-Trigger, Shop-Käufe und externe Integrationen sind noch nicht implementiert.

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

Details und die technischen Tabellen stehen in [docs/architecture/messaging.md](docs/architecture/messaging.md). Die Tests in `FlurNetz.Messaging.IntegrationTests` verwenden echtes PostgreSQL über Testcontainers; Docker oder alternativ `FLURNETZ_TEST_CONNECTION_STRING` ist dafür erforderlich.

## BuildingBlocks und Architekturtests

`FlurNetz.BuildingBlocks` enthält ausschließlich kleine, domain-neutrale Primitives für eine spätere gemeinsame Nutzung. Dazu gehören Result-/Error-Typen, generische Guards, die minimale `IClock`-Abstraktion und deren neutrale `SystemClock`-Implementierung.

Die Projekte `FlurNetz.BuildingBlocks.Tests`, `FlurNetz.Persistence.Tests`, `FlurNetz.Messaging.Tests`, `FlurNetz.Messaging.IntegrationTests`, `FlurNetz.Modules.Identity.Tests`, `FlurNetz.Modules.Identity.IntegrationTests`, `FlurNetz.Modules.Engagement.Tests`, `FlurNetz.Modules.Engagement.IntegrationTests`, `FlurNetz.Modules.Progression.Tests`, `FlurNetz.Modules.Progression.IntegrationTests`, `FlurNetz.Modules.Economy.Tests`, `FlurNetz.Modules.Economy.IntegrationTests`, `FlurNetz.Modules.Rewards.Tests`, `FlurNetz.Modules.Rewards.IntegrationTests`, `FlurNetz.Modules.Inventory.Tests`, `FlurNetz.Modules.Inventory.IntegrationTests`, `FlurNetz.Modules.Titles.Tests`, `FlurNetz.Modules.Titles.IntegrationTests`, `FlurNetz.Modules.Achievements.Tests`, `FlurNetz.Modules.Achievements.IntegrationTests`, `FlurNetz.Modules.Shop.Tests`, `FlurNetz.Modules.Shop.IntegrationTests`, `FlurNetz.Workflows.IntegrationTests`, `FlurNetz.Api.IntegrationTests` und `FlurNetz.Architecture.Tests` prüfen Primitives, Persistence- und Messaging-Logik, Identity- und Engagement-Vertical-Slices, den Rewards-Katalog und die atomare Rewards-Ausführung, die persistierten Inventory-, Progression-, Economy-, Titles-, Achievements- und Shop-Slices einschließlich Nebenläufigkeit, den Ende-zu-Ende-Workflow gegen PostgreSQL, den HTTP-zu-PostgreSQL-Weg sowie Projekt-, Namespace- und Typgrenzen.

## Identity Foundation und erster Vertical Slice

Identity ist das erste Referenzmodul und besitzt die zentrale interne Identität eines Community-Mitglieds. `FlurNetz.Modules.Identity.Contracts` enthält ausschließlich den stabilen, unveränderlichen Identifier `CommunityIdentityId`; `FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity` mit dieser ID.

Der erste Identity-Use-Case erzeugt eine neue `CommunityIdentityId`, bildet die Domain-Identity und persistiert sie in PostgreSQL. Der Dapper-/Npgsql-Adapter arbeitet gegen die Identity-eigene Tabelle `community_identities`, die ausschließlich `id uuid primary key` enthält; Laden über die interne ID ist ebenfalls enthalten. Externe Plattformkennungen werden später über Auflösung und Mapping auf die interne FlurNetz-Identität bezogen. Sie ersetzen `CommunityIdentityId` nicht.

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
`SELECT FOR UPDATE` aus; ein Credit erzeugt den Zustand lazy erst bei Erfolg. `Economy.Contracts`
enthält nun die neutrale, transaction-aware Fähigkeit `IEconomyBalanceCredit`; Economy kennt
Rewards dabei nicht.

Eine konkrete Währungsbezeichnung, Multi-Currency, Messaging, Events, Transfers, Rewards-
Trigger, Shop-Funktionalität und Economy-API sind noch nicht enthalten. Details stehen in
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
Messaging, Events, Inventory-/Title-Rewards, API, Admin UI und Worker-Anbindung sind nicht
Bestandteil dieses Slices. Es gibt noch keinen Runtime-Trigger. Details stehen in
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

`FlurNetz.Modules.Inventory.Contracts` enthält ausschließlich den gemeinsam verwendeten,
stabilen Fachtyp `ItemDefinitionId`. Messaging, Rewards-/Shop-Anbindung, Item-Katalog, API,
Admin UI und Worker sind weiterhin nicht Bestandteil dieses Slices. Details stehen in
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
Current→Unlock-Invariante. `Titles.Contracts` bleibt bewusst leer. Es gibt keine API, kein
Admin UI und keine Reward-, Achievement- oder Shop-Integration; Messaging und Worker sind
nicht an Titles angebunden. Die echten Tests liegen in
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
Rewards-, Economy-, Inventory-, Titles-, Shop-, API- oder Worker-Anbindung. Details stehen in
[docs/architecture/achievements.md](docs/architecture/achievements.md).

## Shop Foundation

`FlurNetz.Modules.Shop` enthält neben dem fachlichen Angebotsfundament jetzt einen vollständig
persistierten internen Katalog. `ShopOfferId` ist der einzige öffentliche Shop-Typ und liegt in
`Shop.Contracts`. `ShopOffer` verwendet die gemeinsame `ItemDefinitionId` aus
`Inventory.Contracts`, einen `ShopPrice`, einen kanonischen Anzeigenamen, eine optionale
Beschreibung, ein halboffenes `AvailabilityWindow`, ein optionales positives Kauflimit pro
Identität und einen Aktivierungszustand. Neue Angebote starten deaktiviert; Ziel-IDs bleiben
unveränderlich, Änderungen erfolgen über gezielte Domainmethoden. `ShopOffer.Rehydrate` stellt
persistierte Angebote mit denselben Domaininvarianten wieder her. Textgrenzen werden nach
Unicode-Skalarwerten passend zur PostgreSQL-Zeichensemantik bewertet; U+0000 und nicht
wohlgeformtes UTF-16 werden abgewiesen. Gesetzte Availability-Grenzen sind kanonische UTC-
Instants mit exakt PostgreSQL-kompatibler Mikrosekundenpräzision.

`Shop:1:CreateShopOffers` besitzt ausschließlich die Tabelle `shop_offers`. Der interne
`IShopOfferStore` wird durch einen gezielten PostgreSQL-/Dapper-Store implementiert; atomare
Katalogmutationen verwenden `SELECT FOR UPDATE`; der Mutation-Callback ist durch
`Task<bool> ExecuteAsync(ShopOfferId, Func<ShopOffer, bool>, CancellationToken)` tatsächlich
synchron. Die Use Cases umfassen Create, Get, List, Rename, Description-, Preis-, Availability-
und Kauflimitänderungen sowie Enable/Disable. Echte PostgreSQL-Integrationstests prüfen
Migration-Scope, Schema, Constraints, Unicode-/Zeit-Roundtrips, Rollback, No-op-Updates und
deterministische Nebenläufigkeit. Käufe, Economy, Inventory Grant, Messaging, API und
Administration sind weiterhin nicht vorhanden. Details stehen in
[docs/architecture/shop.md](docs/architecture/shop.md).

## Persistence Foundation

`FlurNetz.Persistence` verwendet PostgreSQL, Npgsql und Dapper ohne ORM und ohne Generic Repository. Migrationen werden als explizite SQL-Texte von ihren jeweiligen Besitzern bereitgestellt, deterministisch ausgeführt und in `flurnetz_persistence.migration_history` nachverfolgt. Bereits angewendete Migrationen sind unveränderlich; eine abweichende SQL-Checksum führt zu einem Fehler.

`FlurNetz.Persistence.IntegrationTests` testet Verbindungen, Commit/Rollback und den Migration Runner gegen PostgreSQL. Für den automatischen Testlauf wird Docker für Testcontainers benötigt. Alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` auf eine isolierte PostgreSQL-Testdatenbank zeigen.

Identity, Engagement, Progression, Economy, Rewards, Inventory, Titles und Achievements besitzen jeweils eigene fachliche Tabellen und gezielte Adapter; die fachlichen Migrationen laufen über dieselbe technische Persistence Foundation. Engagement persistiert Activity und Outbox atomar. Progression, Economy, Rewards, Inventory und Titles verwenden für konkurrierende beziehungsweise verpflichtende Mutationen atomare Transaktionen und gezielte Zeilensperren; Achievements verwendet einen atomaren Composite-Key-Insert für idempotente Unlocks. Rewards und Economy koordinieren ihre Writes über eine gemeinsame Connection/Transaction und erzeugen keine Cross-Module-Foreign-Keys. Inventory, Titles und Achievements bleiben ebenfalls frei von Cross-Module-Foreign-Keys auf Identity; Achievements besitzt nur einen internen Foreign Key von Community-Achievements auf seine Definition. Der Titles-Katalog liegt in `title_definitions` und besitzt keinen Unlock→Definition-Foreign-Key. API und Worker stellen ihre jeweilige Connection-Konfiguration als unabhängige Composition Roots bereit und führen ihre benötigten Migrationen vor dem Start ihrer Runtime aus; Rewards, Inventory, Titles und Achievements sind noch nicht hostverdrahtet. Der Worker verarbeitet die Outbox kontinuierlich über den bestehenden Processor; Engagement, Progression und Economy sind weiterhin nicht als HTTP-Endpunkte registriert, Rewards, Inventory, Titles und Achievements besitzen weder API noch Worker-Trigger. Externe Plattformintegrationen sind nicht implementiert. Details stehen in [docs/architecture/persistence.md](docs/architecture/persistence.md).

## Fachmodule

Für jedes vorgesehene Fachmodul existieren eine Contracts-Class-Library, eine Implementierungs-Class-Library und ein xUnit-v3-Testprojekt. Die noch nicht begonnenen Module bleiben bewusst leer; Identity bildet mit `CommunityIdentityId`, `CommunityIdentity`, Use Case, gezieltem Persistence-Adapter und Migration den ersten fachlichen Vertical Slice. Engagement ergänzt den Message-Recording-Slice mit eigenem Integration Event und atomarem Activity-/Outbox-Write. Progression ergänzt den persistierten XP-Slice mit atomarem Store, Inbox-Consumer und Parallelitätstests. Economy ergänzt den persistierten Saldo-Slice mit atomarem Store, eigener Migration und Parallelitätstests, bleibt aber ohne Host- oder API-Verdrahtung. Rewards besitzt nun einen persistierten und ausführbaren Domain-/Application-/Persistence-Slice für Economy-Balance-Gutschriften mit eigener Migration, Idempotenz- und Atomicity-Tests, bleibt aber ohne Runtime-Trigger, API und Worker-Verdrahtung. Inventory ergänzt den ersten persistierten Vertical Slice mit atomarem PostgreSQL-Store, eigener Migration und Sparse-Zero-Lifecycle und bleibt ohne Messaging sowie Rewards-/Shop-Anbindung. Titles ergänzt nun zusätzlich zu Rehydration und Community-State einen persistierten Definitionskatalog mit `TitleDefinition`, internen Create/Get/List/Rename/Description-Use-Cases, `Titles:2:CreateTitleDefinitions`, Row-Locking und echten Katalog-Concurrency-Tests. Achievements ergänzt einen persistierten Definitionskatalog und permanente, atomare, idempotente Community-Unlocks mit eigener Migration und Concurrency-Tests. Shop besitzt jetzt mit `Shop:1:CreateShopOffers` einen persistierten Angebotskatalog mit internen Katalog-Use-Cases, gezieltem PostgreSQL-/Dapper-Store, Row-Lock-Mutationen und echten PostgreSQL-Integrationstests. Käufe, Economy, Inventory Grant, Messaging, API und Worker bleiben ausgeschlossen. Der erste Ende-zu-Ende-Workflow läuft über Outbox, Worker, Inbox und Progression-Consumer. Der Worker ist kein Fachmodul. Die Grenzen und die spätere Reihenfolge sind in [docs/architecture/modules.md](docs/architecture/modules.md) beschrieben.

## Lokale API-Ausführung

Voraussetzung sind das in `global.json` festgelegte stabile .NET-10-SDK und eine erreichbare PostgreSQL-Datenbank. Der Host führt die technische Migration-History und die Identity-Migration beim Start aus. Für lokale Zugangsdaten werden User Secrets oder Umgebungsvariablen verwendet; keine Passwörter gehören ins Repository.

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

Der Entwicklungsstand enthält noch kein Authentifizierungssystem und keine Twitch-, Streamer.bot- oder sonstige Plattformintegration.

## Lokale Worker-Ausführung

Der unabhängige Worker-Host verwendet dieselbe Konfigurationskonvention
ConnectionStrings:FlurNetz und benötigt eine erreichbare PostgreSQL-Datenbank. Beim Start führt
er ausschließlich die Messaging- und Progression-Migrationen aus, validiert die explizite
Registry-/Consumer-Komposition und verarbeitet die Outbox danach kontinuierlich.
engagement_activities wird vom Worker nicht angelegt; der Worker referenziert nur den
Engagement-Contract.

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
dotnet test
```

Die Architektur des Hosts ist in [docs/architecture/api.md](docs/architecture/api.md) beschrieben. Die initiale Gesamtrichtung steht in [docs/architecture/overview.md](docs/architecture/overview.md); die Regeln für BuildingBlocks stehen in [docs/architecture/building-blocks.md](docs/architecture/building-blocks.md).
