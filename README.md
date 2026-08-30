# FlurNetz

FlurNetz ist ein modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst eine minimale BuildingBlocks-Grundlage, die technische Persistence Foundation, die Messaging Foundation, die physischen Grenzen der vorgesehenen Fachmodule, den ersten fachlichen Identity-Vertical-Slice, den ersten Engagement-Message-Recording-Slice mit Outbox, den ersten Progression-Inbox-Consumer und den ausführbaren API-Host. Der Cross-Module-Workflow ist Ende zu Ende gegen PostgreSQL getestet; ein dauerhaft laufender Worker, eine Engagement-HTTP-Schnittstelle und externe Integrationen sind noch nicht implementiert.

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

Die PostgreSQL-Outbox wird über dieselbe `PostgreSqlTransaction` wie ein fachlicher Datenbank-Write befüllt. Dadurch sind Business Write und Outbox Insert gemeinsam commit- oder rollbackfähig. Ein aufrufbarer Outbox Processor verwendet PostgreSQL-Leases, Inbox-Deduplizierung pro stabiler Consumer Identity, Retry und einen isolierten Failed/Poison-Status. Es gibt in diesem Schritt keinen externen Broker und keinen Worker Host.

Details und die technischen Tabellen stehen in [docs/architecture/messaging.md](docs/architecture/messaging.md). Die Tests in `FlurNetz.Messaging.IntegrationTests` verwenden echtes PostgreSQL über Testcontainers; Docker oder alternativ `FLURNETZ_TEST_CONNECTION_STRING` ist dafür erforderlich.

## BuildingBlocks und Architekturtests

`FlurNetz.BuildingBlocks` enthält ausschließlich kleine, domain-neutrale Primitives für eine spätere gemeinsame Nutzung. Dazu gehören Result-/Error-Typen, generische Guards, die minimale `IClock`-Abstraktion und deren neutrale `SystemClock`-Implementierung.

Die Projekte `FlurNetz.BuildingBlocks.Tests`, `FlurNetz.Persistence.Tests`, `FlurNetz.Messaging.Tests`, `FlurNetz.Messaging.IntegrationTests`, `FlurNetz.Modules.Identity.Tests`, `FlurNetz.Modules.Identity.IntegrationTests`, `FlurNetz.Modules.Engagement.Tests`, `FlurNetz.Modules.Engagement.IntegrationTests`, `FlurNetz.Modules.Progression.Tests`, `FlurNetz.Modules.Progression.IntegrationTests`, `FlurNetz.Workflows.IntegrationTests`, `FlurNetz.Api.IntegrationTests` und `FlurNetz.Architecture.Tests` prüfen Primitives, Persistence- und Messaging-Logik, Identity- und Engagement-Vertical-Slices, den persistierten Progression-Vertical-Slice einschließlich Nebenläufigkeit, den Ende-zu-Ende-Workflow gegen PostgreSQL, den HTTP-zu-PostgreSQL-Weg sowie Projekt-, Namespace- und Typgrenzen.

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
Duplicate Delivery vergibt dadurch nicht doppelt; Level, Rewards, API-Endpunkte und ein Worker
Host sind weiterhin nicht Bestandteil. Details stehen in [docs/architecture/progression.md](docs/architecture/progression.md).

## Persistence Foundation

`FlurNetz.Persistence` verwendet PostgreSQL, Npgsql und Dapper ohne ORM und ohne Generic Repository. Migrationen werden als explizite SQL-Texte von ihren jeweiligen Besitzern bereitgestellt, deterministisch ausgeführt und in `flurnetz_persistence.migration_history` nachverfolgt. Bereits angewendete Migrationen sind unveränderlich; eine abweichende SQL-Checksum führt zu einem Fehler.

`FlurNetz.Persistence.IntegrationTests` testet Verbindungen, Commit/Rollback und den Migration Runner gegen PostgreSQL. Für den automatischen Testlauf wird Docker für Testcontainers benötigt. Alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` auf eine isolierte PostgreSQL-Testdatenbank zeigen.

Identity, Engagement und Progression besitzen jeweils eine eigene fachliche Tabelle und einen gezielten Adapter; die fachlichen Migrationen laufen über dieselbe technische Persistence Foundation. Engagement persistiert Activity und Outbox atomar. Progression verwendet für konkurrierende XP-Vergaben eine atomare Transaktion mit Zeilensperre und erzeugt keinen Cross-Module-Foreign-Key auf Identity. Der API-Host stellt die Connection-Konfiguration als Composition Root bereit und führt den bestehenden Migration Runner vor dem Listener-Start aus; der Outbox-Processor wird im Workflow-Test direkt aufgerufen, aber noch nicht als Worker betrieben. Engagement und Progression sind weiterhin nicht als HTTP-Endpunkte registriert. Externe Plattformintegrationen sind nicht implementiert. Details stehen in [docs/architecture/persistence.md](docs/architecture/persistence.md).

## Fachmodule

Für jedes vorgesehene Fachmodul existieren eine Contracts-Class-Library, eine Implementierungs-Class-Library und ein xUnit-v3-Testprojekt. Die übrigen Module bleiben bewusst leer; Identity bildet mit `CommunityIdentityId`, `CommunityIdentity`, Use Case, gezieltem Persistence-Adapter und Migration den ersten fachlichen Vertical Slice. Engagement ergänzt den Message-Recording-Slice mit eigenem Integration Event und atomarem Activity-/Outbox-Write. Progression ergänzt den persistierten XP-Slice mit atomarem Store, Inbox-Consumer und Parallelitätstests. Der erste Ende-zu-Ende-Workflow läuft über Outbox und Inbox; ein dauerhaft laufender Processor-Host existiert noch nicht. Die Grenzen und die spätere Reihenfolge sind in [docs/architecture/modules.md](docs/architecture/modules.md) beschrieben.

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

## Gesamte lokale Prüfung

Für die vollständige Prüfung sind Docker für Testcontainers oder alternativ eine isolierte PostgreSQL-Datenbank über `FLURNETZ_TEST_CONNECTION_STRING` erforderlich.

```text
dotnet restore
dotnet build
dotnet test
```

Die Architektur des Hosts ist in [docs/architecture/api.md](docs/architecture/api.md) beschrieben. Die initiale Gesamtrichtung steht in [docs/architecture/overview.md](docs/architecture/overview.md); die Regeln für BuildingBlocks stehen in [docs/architecture/building-blocks.md](docs/architecture/building-blocks.md).
