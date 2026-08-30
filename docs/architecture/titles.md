# Titles Foundation

## Verantwortung

Das Titles-Modul besitzt den fachlichen Titelzustand einer internen
`CommunityIdentityId`. Es verwaltet die Titelberechtigungen dieser Community-Identität
und die optionale aktuelle Titelauswahl. Externe Plattformidentitäten werden nicht im
Titles-Modul modelliert.

## `TitleDefinitionId`

`TitleDefinitionId` ist eine unveränderliche, Guid-basierte und stabile fachliche
Kennung. Sie identifiziert eine Titeldefinition ausschließlich über ihre ID. Namen,
Anzeigenamen, Beschreibungen, Icons, Farben, CSS, Badges, Kategorien, Seltenheit,
Sortierung, Übersetzungen, Sichtbarkeit und Unlock-Bedingungen sind keine Bestandteile
dieser Foundation.

Ein Titelkatalog beziehungsweise ein `TitleDefinition`-Aggregat wird erst mit einem
konkreten späteren Bedarf modelliert.

## `CommunityTitles`

`CommunityTitles` gehört genau einer gültigen `CommunityIdentityId`. Ein neuer Zustand
startet ohne freigeschaltete Titel und ohne aktuelle Auswahl. Die freigeschalteten
`TitleDefinitionId`-Werte werden intern eindeutig gehalten und nach außen nur als
schreibgeschützter Snapshot lesbar gemacht.

Die Domain bietet folgende Operationen:

- `Unlock` schaltet einen Titel idempotent frei und wählt ihn nicht automatisch aus.
- `Lock` entfernt eine Titelberechtigung idempotent.
- `SetCurrent` wählt einen freigeschalteten Titel aus und ersetzt eine bestehende Auswahl.
- `ClearCurrent` entfernt die aktuelle Auswahl, ohne Freischaltungen zu verändern.

Die Rückgabewerte zeigen an, ob sich der Zustand tatsächlich verändert hat. Das Setzen
eines nicht freigeschalteten Titels wird mit `TitleNotUnlockedException` abgelehnt.

## Invarianten

- Eine Community-Identität kann null bis beliebig viele unterschiedliche Titel besitzen.
- Es gibt höchstens eine aktuelle Titelauswahl.
- Ein aktuell ausgewählter Titel ist immer Teil der freigeschalteten Titelmenge.
- Das Sperren des aktuellen Titels entfernt daher gleichzeitig die aktuelle Auswahl.
- Fehlgeschlagene Operationen verändern den bestehenden Zustand nicht.

## Contracts

`FlurNetz.Modules.Titles.Contracts` bleibt in dieser Foundation vollständig leer. Es gibt
noch keinen Cross-Module-Caller, der einen öffentlichen Titles-Contract benötigt.

`FlurNetz.Modules.Titles` referenziert neben dem eigenen leeren Contracts-Projekt
ausschließlich `FlurNetz.Modules.Identity.Contracts` und verwendet daraus die zentrale
`CommunityIdentityId`.

## Bewusst nicht enthalten

- Persistence, PostgreSQL, SQL, Dapper, Migrationen oder Rehydration
- Repository, Store, Application Use Cases oder Modulregistrierung
- Messaging, Domain Events, Integration Events, Inbox oder Outbox
- Rewards-, Achievement-, Shop-, Inventory-, Progression- oder Economy-Anbindung
- API, Controller, Endpoints, Admin UI, Worker oder Overlay
- Titelkatalog und Darstellungsmetadaten
- Plattformidentitäten oder externe Integrationen

## Spätere Slices

Persistence und konkrete Cross-Module-Kompositionen werden erst bei einem konkreten
fachlichen Bedarf in eigenen Slices ergänzt. Diese Foundation nimmt weder eine spätere
Tabellenstruktur noch eine Eventstruktur vorweg.
