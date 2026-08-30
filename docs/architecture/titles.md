# Titles Foundation

## Verantwortung

Das Titles-Modul besitzt die fachliche Zuordnung freigeschalteter Titel zu einer internen
`CommunityIdentityId` sowie die optionale aktuelle Auswahl. Die Foundation modelliert bewusst
noch keinen Titelkatalog. Ein Titel wird zunächst ausschließlich durch seine stabile
`TitleDefinitionId` identifiziert.

## TitleDefinitionId

`TitleDefinitionId` ist ein unveränderlicher Guid-basierter Fachtyp. Leere GUIDs werden
abgelehnt. Die Kennung enthält keine Anzeigenamen, Beschreibungen, Farben, Icons, Kategorien,
Seltenheiten oder andere UI-/Katalogmetadaten.

Eine konkrete `TitleDefinition` wird in dieser Foundation nicht eingeführt. Katalogdaten
werden erst modelliert, wenn ein realer Konfigurations-, Anzeige- oder Administrations-Use-Case
sie benötigt.

## CommunityTitles

`CommunityTitles` gehört genau einer gültigen `CommunityIdentityId`. Ein neuer Zustand startet
ohne freigeschaltete Titel und ohne aktuelle Auswahl.

Verbindliche Invarianten:

- Eine Community-Identität kann null bis beliebig viele unterschiedliche Titel freigeschaltet haben.
- `Unlock` ist idempotent; eine doppelte Freischaltung erzeugt keinen doppelten Zustand.
- Eine Freischaltung wählt den Titel nicht automatisch aus.
- Höchstens ein Titel kann aktuell ausgewählt sein.
- Der aktuelle Titel muss bereits freigeschaltet sein.
- Die Auswahl eines nicht freigeschalteten Titels schlägt mit `TitleNotUnlockedException` fehl.
- Die aktuelle Auswahl kann entfernt werden, ohne Freischaltungen zu verändern.
- Freischaltungen werden als schreibgeschützte Snapshots nach außen gegeben und können den internen Zustand nicht mutieren.

## Bewusst keine Lock-/Revoke-Semantik

Die Foundation kennt noch kein Entziehen, Sperren oder Zurücknehmen eines bereits
freigeschalteten Titels. Dafür existiert aktuell kein konkreter fachlicher Caller. Insbesondere
wird keine spätere Rewards-, Achievement- oder Admin-Semantik vorweggenommen.

Wenn ein realer Use Case eine Entziehung benötigt, muss dann ausdrücklich entschieden werden,
wie sich eine Entziehung des aktuell ausgewählten Titels verhält.

## Modulgrenzen

`FlurNetz.Modules.Titles` referenziert ausschließlich:

- `FlurNetz.Modules.Titles.Contracts`
- `FlurNetz.Modules.Identity.Contracts`

`FlurNetz.Modules.Titles.Contracts` bleibt bewusst leer. Die Foundation benötigt noch keinen
öffentlichen Cross-Module-Vertrag.

## Bewusst nicht enthalten

- Persistence, Migrationen, Store oder Repository
- Rehydration
- Messaging, Domain Events, Integration Events, Inbox oder Outbox
- Rewards- oder Achievement-Anbindung
- Shop-Anbindung
- Titelkatalog mit Name, Beschreibung, Farbe, Icon, Kategorie oder Seltenheit
- zeitlich begrenzte Titel oder Ablaufdaten
- Titel-Hierarchien, Gruppen oder Prioritäten
- Lock-, Revoke- oder Remove-Use-Cases
- API
- Admin UI
- Worker
- Plattformidentitäten oder externe Integrationen

Diese Grenzen bleiben bestehen, bis ein konkreter späterer Slice einen engeren Bedarf belegt.
