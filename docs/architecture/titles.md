# Titles-Vertical-Slice

## Verantwortung

Das Titles-Modul besitzt den fachlichen Titelzustand einer internen
`CommunityIdentityId`. Es verwaltet die Titelberechtigungen dieser Community-Identität
und die optionale aktuelle Titelauswahl. Der erste persistierte Vertical Slice speichert
diesen Zustand unabhängig in PostgreSQL. Externe Plattformidentitäten werden nicht im
Titles-Modul modelliert.

## `TitleDefinitionId`

`TitleDefinitionId` ist eine unveränderliche, Guid-basierte und stabile fachliche
Kennung. Sie identifiziert eine Titeldefinition ausschließlich über ihre ID. Namen,
Anzeigenamen, Beschreibungen, Icons, Farben, CSS, Badges, Kategorien, Seltenheit,
Sortierung, Übersetzungen, Sichtbarkeit und Unlock-Bedingungen sind keine Bestandteile
dieses Slices.

Ein Titelkatalog beziehungsweise ein `TitleDefinition`-Aggregat wird erst mit einem
konkreten späteren Bedarf modelliert. Es gibt noch keine `title_definitions`-Tabelle.

## `CommunityTitles` und Rehydration

`CommunityTitles` gehört genau einer gültigen `CommunityIdentityId`. `Create` erzeugt
einen neuen leeren Zustand. `Rehydrate` rekonstruiert dagegen einen bereits gespeicherten
Zustand aus der Freischaltungsmenge und der optionalen aktuellen Auswahl. Die übergebene
Collection wird in eine eigene Set-Repräsentation kopiert; Duplikate werden vereinheitlicht.
Ein Current, der nicht freigeschaltet ist, führt mit `TitleNotUnlockedException` zu einem
sichtbaren Fehler. Der beschädigte Zustand wird nicht automatisch repariert.

Die Domain bietet folgende Operationen:

- `Unlock` schaltet einen Titel idempotent frei und wählt ihn nicht automatisch aus.
- `Lock` entfernt eine Titelberechtigung idempotent.
- `SetCurrent` wählt einen freigeschalteten Titel aus und ersetzt eine bestehende Auswahl.
- `ClearCurrent` entfernt die aktuelle Auswahl, ohne Freischaltungen zu verändern.

Die Rückgabewerte zeigen an, ob sich der Zustand tatsächlich verändert hat. Ein aktuell
ausgewählter Titel ist immer freigeschaltet; das Sperren des aktuellen Titels entfernt
daher zugleich die Auswahl.

## Application und atomarer Store

Die interne Application-Schicht enthält `UnlockCommunityTitle`, `LockCommunityTitle`,
`SetCurrentCommunityTitle` und `ClearCurrentCommunityTitle`. Sie delegieren die
fachliche Operation an `ICommunityTitlesStore` und enthalten keine SQL-, Locking- oder
Invariantenlogik.

`CommunityTitlesStore` öffnet eine `PostgreSqlTransaction`, validiert die Community-ID,
legt die Root-Zeile bei Bedarf mit `ON CONFLICT DO NOTHING` an und sperrt sie anschließend
mit `SELECT FOR UPDATE`. Danach werden Unlocks und Current geladen, über `Rehydrate`
in die Domain überführt und vor dem synchronen Domain-Callback als eigene Snapshots
gesichert. Nur der Zustands-Diff wird zurückgeschrieben. Neue Unlocks werden zuerst
eingefügt, danach wird die Selection synchronisiert und zuletzt werden entfernte Unlocks
gelöscht. Commit und Rückgabewert erfolgen erst nach erfolgreicher Persistierung.

Jede Exception einschließlich `TitleNotUnlockedException` und Cancellation führt zum
Rollback; auch eine beim ersten Zugriff erzeugte Root-Zeile wird dann nicht committed.
Der synchrone Callback ist bewusst nicht asynchron, damit keine beliebige externe I/O in
der offenen Titles-Transaktion ausgeführt werden kann.

## PostgreSQL-Schema

Titles besitzt im bestehenden `public`-Schema genau drei eigene Tabellen:

- `community_titles` mit `community_identity_id` als Aggregate- und Lock-Schlüssel
- `community_title_unlocks` mit dem Composite Primary Key aus Community- und Titel-ID
- `community_title_selections` mit genau einer möglichen Selection pro Community

Die Titles-Tabellen besitzen interne Foreign Keys auf den Root und von der Selection auf
einen passenden Unlock. Dadurch ist die Invariante Current → Unlock zusätzlich in der
Datenbank geschützt. `community_identity_id` bleibt ein fachlicher Cross-Module-Identifier;
es gibt keinen Foreign Key auf `community_identities` oder Tabellen anderer Module.
Eine `title_definitions`-Tabelle wird in diesem Slice nicht angelegt.

Der Root-Lock serialisiert Operationen pro `CommunityIdentityId`. Unterschiedliche
Communities können parallel verarbeitet werden. Es wird kein künstliches
`SERIALIZABLE` und kein globaler Advisory Lock verwendet.

## Contracts und Grenzen

`FlurNetz.Modules.Titles.Contracts` bleibt vollständig leer. `TitleDefinitionId` und alle
anderen Domain-, Application-, Persistence-, Migration- und Registrierungs-Typen bleiben
implementation-owned. Das Titles-Projekt referenziert neben dem leeren eigenen Contract
und `FlurNetz.Modules.Identity.Contracts` ausschließlich `FlurNetz.Persistence`.

Nicht enthalten sind Messaging, Domain Events, Integration Events, Inbox, Outbox,
Rewards-, Achievements-, Shop-, Inventory-, Progression- oder Economy-Anbindung, API,
Controller, Admin UI, Worker, Overlay, Plattformintegrationen und ein Titelkatalog.
Der Slice ist noch nicht in API oder Worker verdrahtet.

## Tests

Die Domain- und Application-Unit-Tests prüfen Rehydration, Kopieren und Validierung des
Zustands, die eingefrorenen Invarianten und die reine Store-Delegation. Die
Architecture Tests prüfen Ownership, leere Contracts, Namespace- und
Projektabhängigkeitsgrenzen, Migration und Modulregistrierung.

`FlurNetz.Modules.Titles.IntegrationTests` verwendet echtes PostgreSQL über
Testcontainers (`postgres:15.1`) oder `FLURNETZ_TEST_CONNECTION_STRING`. Es prüft
Migration und Idempotenz, die vier atomaren Operationen, FK-Constraints, Rehydration,
Rollback, Isolation und konkurrierende Änderungen derselben sowie verschiedener
Communities.
