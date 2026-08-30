# FlurNetz.Api

## Rolle des Hosts

`FlurNetz.Api` ist ein eigenständiger ausführbarer FlurNetz-Host und ausschließlich Composition
Root und HTTP-Adapter. Er konfiguriert den ASP.NET-Core-Host, liest die PostgreSQL-
Konfiguration, registriert die technische Persistence Foundation, bindet das Identity-Modul
ein, führt die Startmigrationen aus und ordnet HTTP-Endpunkte zu. Der unabhängige
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

Die erlaubte Richtung lautet:

`FlurNetz.Api` → `FlurNetz.Modules.Identity` → `FlurNetz.Persistence`

`FlurNetz.Persistence`, `FlurNetz.Messaging`, `FlurNetz.BuildingBlocks`, Contracts und
Fachmodule referenzieren die API nicht. Weitere Fachmodule werden zur Laufzeit nicht
registriert. Messaging ist deshalb weder Project Reference noch Runtime-Bestandteil dieses
Hosts; die Outbox-Verarbeitung läuft ausschließlich im unabhängigen `FlurNetz.Worker`.

## PostgreSQL und Startup

Die Verbindung wird über `ConnectionStrings:FlurNetz` konfiguriert. Der Repository-Stand
enthält nur einen leeren Wert als sicheren Basiseintrag. Lokale Werte werden über User Secrets
oder die Umgebungsvariable `ConnectionStrings__FlurNetz` bereitgestellt.

Der Host verwendet die vorhandene `PostgreSqlConnectionFactory` und erzeugt keine zweite
`NpgsqlDataSource`-, Connection- oder Transaction-Infrastruktur. Vor dem Start des HTTP-
Listeners löst der Host den bestehenden `MigrationRunner` auf und führt die registrierten
Migrationsquellen aus. In diesem Host ist das die Identity-Quelle; die technische
`flurnetz_persistence.migration_history` wird vom Runner selbst verwaltet. Schlägt die
Verbindung oder eine Migration fehl, wird der Fehler mit ASP.NET-Core-Logging auf Critical-
Ebene geloggt und der Startup abgebrochen.

## HTTP-Endpunkt

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

## Fehlerbehandlung und aktueller Umfang

Der Host verwendet `AddProblemDetails()`, in Development die Developer Exception Page und
außerhalb von Development `UseExceptionHandler()`. Damit werden ungefangene technische
Fehler nicht als unkontrollierte Stacktrace-Antwort ausgeliefert.

Aktuell gibt es bewusst:

- keine Authentifizierung oder Autorisierung
- kein JWT, Cookie, OAuth oder Identity Framework
- kein Messaging Runtime Processing, keine Outbox-Loop und keinen Worker im API-Prozess
- keine Twitch-, Streamer.bot-, Discord-, YouTube- oder Kick-Integration
- keine weiteren fachlichen Module im API-Host

## Tests

`FlurNetz.Api.IntegrationTests` verwendet `WebApplicationFactory` für den echten API-Host und
Testcontainers PostgreSQL. Die Testkonfiguration überschreibt den Connection String über den
ASP.NET-Core-Testhost; Produktionskonfiguration und Secrets werden nicht verändert.

Die Tests prüfen:

- Startup gegen eine leere PostgreSQL-Datenbank inklusive Identity-Migration
- `POST /api/identities` mit `201 Created` und gültiger ID
- Übereinstimmung zwischen Response-ID und `community_identities`
- mehrere Requests mit unterschiedlichen IDs und persistierten Datensätzen
- Startup-Abbruch bei nicht erreichbarer PostgreSQL-Datenbank

Die Architekturtests prüfen zusätzlich die erlaubten Host-Referenzen, die verbotene Richtung
`*` → `FlurNetz.Api` und dass der API-Host keine Repository-, Domain- oder Migrationstypen
enthält.
