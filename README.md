# FlurNetz

FlurNetz ist ein modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst eine minimale BuildingBlocks-Grundlage, die technische Persistence Foundation, die Messaging Foundation, die physischen Grenzen der vorgesehenen Fachmodule sowie die erste fachliche Identity Foundation. Die übrigen Fachmodule, Hosts und externe Integrationen sind noch nicht implementiert.

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

Die Projekte `FlurNetz.BuildingBlocks.Tests`, `FlurNetz.Persistence.Tests`, `FlurNetz.Messaging.Tests`, `FlurNetz.Messaging.IntegrationTests`, `FlurNetz.Modules.Identity.Tests` und `FlurNetz.Architecture.Tests` prüfen Primitives, Persistence- und Messaging-Logik, die Identity Foundation sowie Projekt-, Namespace- und Typgrenzen.

## Identity Foundation

Identity ist das erste Referenzmodul und besitzt die zentrale interne Identität eines Community-Mitglieds. `FlurNetz.Modules.Identity.Contracts` enthält ausschließlich den stabilen, unveränderlichen Identifier `CommunityIdentityId`; `FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity` mit dieser ID.

Externe Plattformkennungen werden später über Auflösung und Mapping auf die interne FlurNetz-Identität bezogen. Sie ersetzen `CommunityIdentityId` nicht. Die Foundation enthält noch keinen Create-Use-Case, keine fachliche Persistenz, keine Plattformkonten, keine Authentifizierung, keine API und keine fachlichen Events. Details stehen in [docs/architecture/identity.md](docs/architecture/identity.md).

## Persistence Foundation

`FlurNetz.Persistence` verwendet PostgreSQL, Npgsql und Dapper ohne ORM und ohne Generic Repository. Migrationen werden als explizite SQL-Texte von ihren jeweiligen Besitzern bereitgestellt, deterministisch ausgeführt und in `flurnetz_persistence.migration_history` nachverfolgt. Bereits angewendete Migrationen sind unveränderlich; eine abweichende SQL-Checksum führt zu einem Fehler.

`FlurNetz.Persistence.IntegrationTests` testet Verbindungen, Commit/Rollback und den Migration Runner gegen PostgreSQL. Für den automatischen Testlauf wird Docker für Testcontainers benötigt. Alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` auf eine isolierte PostgreSQL-Testdatenbank zeigen.

Es gibt weiterhin keine fachlichen Modulimplementierungen, fachlichen Tabellen oder fachlichen Repositories. API, Worker und externe Plattformintegrationen sind nicht implementiert. Details stehen in [docs/architecture/persistence.md](docs/architecture/persistence.md).

## Fachmodule

Für jedes vorgesehene Fachmodul existieren eine Contracts-Class-Library, eine Implementierungs-Class-Library und ein minimales xUnit-v3-Testprojekt. Die übrigen Module bleiben bewusst leer; Identity bildet mit `CommunityIdentityId` und `CommunityIdentity` die erste fachliche Foundation. Die Implementierungen referenzieren ausschließlich das jeweils eigene Contracts-Projekt; Cross-Module-Referenzen und fachliche Typen der übrigen Module sind noch nicht vorhanden. Die Grenzen und die spätere Reihenfolge sind in [docs/architecture/modules.md](docs/architecture/modules.md) beschrieben.

## Lokale Entwicklung

Voraussetzung ist das in `global.json` festgelegte stabile .NET-10-SDK.

```text
dotnet restore
dotnet build
dotnet test
```

Die initiale Architektur-Richtung ist in [docs/architecture/overview.md](docs/architecture/overview.md) beschrieben. Die Regeln für BuildingBlocks stehen in [docs/architecture/building-blocks.md](docs/architecture/building-blocks.md).
