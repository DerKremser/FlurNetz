# FlurNetz

FlurNetz ist ein modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst eine minimale BuildingBlocks-Grundlage, die technische Persistence Foundation, die Messaging Foundation, die physischen Grenzen der vorgesehenen Fachmodule, den ersten fachlichen Identity-Vertical-Slice und den ersten ausführbaren API-Host. Ein Worker und externe Integrationen sind noch nicht implementiert.

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

`FlurNetz.BuildingBlocks` enthält ausschließlich kleine, domain-neutrale Primitives für eine spätere gemeinsame Nutzung. Dazu gehören Result-/Error-Typen, generische Guards und die minimale `IClock`-Abstraktion.

Die Projekte `FlurNetz.BuildingBlocks.Tests`, `FlurNetz.Persistence.Tests`, `FlurNetz.Messaging.Tests`, `FlurNetz.Messaging.IntegrationTests`, `FlurNetz.Modules.Identity.Tests`, `FlurNetz.Modules.Identity.IntegrationTests`, `FlurNetz.Api.IntegrationTests` und `FlurNetz.Architecture.Tests` prüfen Primitives, Persistence- und Messaging-Logik, den Identity-Vertical-Slice, den HTTP-zu-PostgreSQL-Weg sowie Projekt-, Namespace- und Typgrenzen.

## Identity Foundation und erster Vertical Slice

Identity ist das erste Referenzmodul und besitzt die zentrale interne Identität eines Community-Mitglieds. `FlurNetz.Modules.Identity.Contracts` enthält ausschließlich den stabilen, unveränderlichen Identifier `CommunityIdentityId`; `FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity` mit dieser ID.

Der erste Identity-Use-Case erzeugt eine neue `CommunityIdentityId`, bildet die Domain-Identity und persistiert sie in PostgreSQL. Der Dapper-/Npgsql-Adapter arbeitet gegen die Identity-eigene Tabelle `community_identities`, die ausschließlich `id uuid primary key` enthält; Laden über die interne ID ist ebenfalls enthalten. Externe Plattformkennungen werden später über Auflösung und Mapping auf die interne FlurNetz-Identität bezogen. Sie ersetzen `CommunityIdentityId` nicht.

Der bestehende `CreateCommunityIdentity`-Use-Case ist über `FlurNetz.Api` als `POST /api/identities` erreichbar. Der HTTP-Adapter akzeptiert keinen Request-Body und gibt bei Erfolg ausschließlich ein API-Response-DTO mit der erzeugten ID zurück. Plattformkonten, Authentifizierung, Profile und fachliche Domain- oder Integration Events sind weiterhin nicht enthalten. Details stehen in [docs/architecture/identity.md](docs/architecture/identity.md) und [docs/architecture/api.md](docs/architecture/api.md).

## Persistence Foundation

`FlurNetz.Persistence` verwendet PostgreSQL, Npgsql und Dapper ohne ORM und ohne Generic Repository. Migrationen werden als explizite SQL-Texte von ihren jeweiligen Besitzern bereitgestellt, deterministisch ausgeführt und in `flurnetz_persistence.migration_history` nachverfolgt. Bereits angewendete Migrationen sind unveränderlich; eine abweichende SQL-Checksum führt zu einem Fehler.

`FlurNetz.Persistence.IntegrationTests` testet Verbindungen, Commit/Rollback und den Migration Runner gegen PostgreSQL. Für den automatischen Testlauf wird Docker für Testcontainers benötigt. Alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` auf eine isolierte PostgreSQL-Testdatenbank zeigen.

Identity besitzt als erstes Modul eine fachliche Tabelle und einen gezielten Repository-Adapter. Weitere fachliche Modulimplementierungen, Tabellen oder Repositories gibt es nicht. Der API-Host stellt die Connection-Konfiguration als Composition Root bereit und führt den bestehenden Migration Runner vor dem Listener-Start aus. Worker und externe Plattformintegrationen sind nicht implementiert. Details stehen in [docs/architecture/persistence.md](docs/architecture/persistence.md).

## Fachmodule

Für jedes vorgesehene Fachmodul existieren eine Contracts-Class-Library, eine Implementierungs-Class-Library und ein xUnit-v3-Testprojekt. Die übrigen Module bleiben bewusst leer; Identity bildet mit `CommunityIdentityId`, `CommunityIdentity`, Use Case, gezieltem Persistence-Adapter und Migration den ersten fachlichen Vertical Slice. Cross-Module-Referenzen und fachliche Typen der übrigen Module sind noch nicht vorhanden. Die Grenzen und die spätere Reihenfolge sind in [docs/architecture/modules.md](docs/architecture/modules.md) beschrieben.

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
