# Titles-Vertical-Slice

## Verantwortung

Das Titles-Modul besitzt sowohl den implementation-eigenen Definitionskatalog als auch den
fachlichen Titelzustand einer internen `CommunityIdentityId`. Der erste Katalog-Slice
persistiert Definitionen mit Anzeigename und optionaler Beschreibung; der bestehende
Community-Slice persistiert Titelberechtigungen und die optionale aktuelle Auswahl.
Externe Plattformidentitäten werden nicht im Titles-Modul modelliert.

## `TitleDefinitionId`

`TitleDefinitionId` ist eine unveränderliche, Guid-basierte und stabile fachliche
Kennung. Sie identifiziert eine Titeldefinition ausschließlich über ihre ID. Sie ist
zugleich die Primärschlüssel-Identität des Katalogs und der Identifier, den der
Community-State für Unlocks verwendet.

## `TitleDefinition` und Definitionskatalog

`TitleDefinition` enthält in diesem Slice ausschließlich `TitleDefinitionId`, einen
normalisierten `DisplayName` und eine optionale normalisierte `Description`. Anzeigenamen
werden getrimmt und dürfen höchstens 100 `string.Length` Zeichen enthalten. Leere oder
nur aus Whitespace bestehende Werte sind ungültig. Beschreibungen werden bei `null`, leerem
oder nur aus Whitespace bestehendem Input kanonisch als `null` gespeichert und dürfen
höchstens 500 Zeichen enthalten. Es gibt keine automatische Kürzung.

`Create` und `Rehydrate` erzwingen dieselben Invarianten. `Rename` und
`ChangeDescription` normalisieren vor dem Vergleich und liefern bei einem kanonischen
No-op `false`. Der interne Katalog bietet die Use Cases `CreateTitleDefinition`,
`RenameTitleDefinition`, `ChangeTitleDescription`, `GetTitleDefinition` und
`ListTitleDefinitions`. Unbekannte Definitionen liefern beim Lesen `null`; eine unbekannte
Definition bei einer Mutation führt zu `TitleDefinitionNotFoundException`.

`ITitleDefinitionStore` ist ein separater interner Store-Port. `TitleDefinitionStore`
verwendet Dapper und PostgreSQL. `Get` und `List` sind einfache Read-Pfade; Mutationen
laden die Zeile mit `SELECT FOR UPDATE`, führen den synchronen Domain-Callback aus und
schreiben nur bei tatsächlicher Änderung innerhalb derselben Transaktion. Dadurch werden
Mutationen derselben Definition serialisiert und Lost Updates verhindert. Unterschiedliche
Definitionen werden nicht global serialisiert.

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

Titles besitzt im bestehenden `public`-Schema vier eigene Tabellen:

- `community_titles` mit `community_identity_id` als Aggregate- und Lock-Schlüssel
- `community_title_unlocks` mit dem Composite Primary Key aus Community- und Titel-ID
- `community_title_selections` mit genau einer möglichen Selection pro Community
- `title_definitions` mit `id`, `display_name` und optionaler `description`

Die Titles-Tabellen besitzen interne Foreign Keys auf den Root und von der Selection auf
einen passenden Unlock. Dadurch ist die Invariante Current → Unlock zusätzlich in der
Datenbank geschützt. `community_identity_id` bleibt ein fachlicher Cross-Module-Identifier;
es gibt keinen Foreign Key auf `community_identities` oder Tabellen anderer Module. Die
`title_definitions`-Tabelle besitzt selbst keine Foreign Keys. Insbesondere existiert in
diesem Slice kein Unlock→Definition-Foreign-Key und keine verpflichtende Policy, nach der
ein Unlock zuerst im Katalog vorhanden sein muss. Die bestehende Migration
`Titles:1:CreateCommunityTitles` bleibt unverändert; `Titles:2:CreateTitleDefinitions`
legt ausschließlich die Katalogtabelle mit kanonischen Text-Checks an.

Der Root-Lock serialisiert Operationen pro `CommunityIdentityId`. Unterschiedliche
Communities können parallel verarbeitet werden. Es wird kein künstliches
`SERIALIZABLE` und kein globaler Advisory Lock verwendet.

## Contracts und Grenzen

`FlurNetz.Modules.Titles.Contracts` bleibt vollständig leer. `TitleDefinitionId` und alle
anderen Domain-, Application-, Persistence-, Migration- und Registrierungs-Typen bleiben
implementation-owned. Das Titles-Projekt referenziert neben dem leeren eigenen Contract
und `FlurNetz.Modules.Identity.Contracts` ausschließlich `FlurNetz.Persistence`.

Nicht enthalten sind Delete, Soft Delete, Archive, Enable/Disable, Visibility, Slug,
TechnicalName, Icon, Farbe, Badge, Rarity, Kategorie, SortOrder, Localization,
Unlock Conditions, Preise, Messaging, Domain Events, Integration Events, Inbox, Outbox,
Rewards-, Achievements-, Shop-, Inventory-, Progression- oder Economy-Anbindung, API,
Controller, Admin UI, Worker, Overlay und Plattformintegrationen.
Der Slice ist noch nicht in API oder Worker verdrahtet.

## Tests

Die Domain- und Application-Unit-Tests prüfen Community-Rehydration, die
TitleDefinition-Invarianten, Create/Rename/ChangeDescription, NotFound und die reine
Store-Delegation. Die Architecture Tests prüfen Ownership, leere Contracts, Namespace-
und Projektabhängigkeitsgrenzen, beide Migrationen und die Modulregistrierung.

`FlurNetz.Modules.Titles.IntegrationTests` verwendet echtes PostgreSQL über
Testcontainers (`postgres:15.1`) oder `FLURNETZ_TEST_CONNECTION_STRING`. Es prüft
Migration und Idempotenz, beide Titles-Bereiche, Katalog-Constraints, die vier
Community-Operationen, Rehydration, Rollback, NotFound und echte konkurrierende
Katalogmutationen mit `SELECT FOR UPDATE`.
