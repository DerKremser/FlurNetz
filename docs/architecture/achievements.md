# Achievements-Vertical-Slice

## Verantwortung

Das Achievements-Modul besitzt in diesem ersten Slice einen implementation-eigenen
Achievement-Definitionskatalog sowie dauerhaft persistierte Community-Achievements. Es kennt
keine fremde Modulimplementierung und liest keine Identity-Tabelle. Eine strukturell gültige
`CommunityIdentityId` darf daher auch dann verwendet werden, wenn die zugehörige Identity-
Zeile noch nicht persistiert ist.

Der Slice ist intern ausführbar und persistiert, besitzt aber bewusst keinen Runtime-Trigger.
Progress, Counter, Regeln und Conditions folgen erst in späteren, separat begründeten Slices.

## Domain

### `AchievementDefinitionId`

`AchievementDefinitionId` ist eine immutable, Guid-basierte und implementation-owned
Fachkennung. `Create(Guid)` akzeptiert nur eine nicht leere GUID; `New()` erzeugt eine neue
Kennung. Weitere Metadaten gehören nicht in diesen Identifier.

### `AchievementDefinition`

`AchievementDefinition` enthält ausschließlich `Id`, `DisplayName` und `Description`.
Anzeigenamen dürfen nicht `null`, leer oder ausschließlich Whitespace sein. Aufrufer-Input wird
mit der .NET-`Trim()`-Semantik normalisiert und darf nach der Normalisierung höchstens 100
`string.Length`-Zeichen enthalten. Beschreibungen werden bei `null`, leerem oder ausschließlich
Whitespace bestehendem Input kanonisch als `null` gespeichert; sonst werden sie ebenfalls
getrimmt und dürfen höchstens 500 Zeichen enthalten. Es gibt keine automatische Kürzung.

`Create` und `Rehydrate` erzwingen dieselben fachlichen Grenzen. `Create` normalisiert neuen
Aufrufer-Input. `Rehydrate` akzeptiert nur bereits kanonische Persistenzwerte und weist
ungetrimmte oder nicht kanonische Daten zurück, damit ein beschädigter Persistenzzustand nicht
still repariert wird. `Rename` und `ChangeDescription` normalisieren den neuen Aufrufer-Input,
vergleichen anschließend kanonisch und liefern bei einem No-op `false`.

### `CommunityAchievement`

`CommunityAchievement` modelliert ein bereits erreichtes Achievement und enthält ausschließlich
`CommunityIdentityId`, `AchievementDefinitionId` und `UnlockedAtUtc`. Es gibt keine zusätzliche
Unlock-ID und nach `Create` beziehungsweise `Rehydrate` keine Mutation.

Beide Identifier werden strukturell validiert. `UnlockedAtUtc` muss einen UTC-Offset von null
besitzen; Nicht-UTC-Werte werden sichtbar abgelehnt. Die Domain prüft nicht, ob die Community-
Identität in Identity existiert.

## Application-Grenzen

Der interne Definitionskatalog bietet die Use Cases `CreateAchievementDefinition`,
`GetAchievementDefinition`, `ListAchievementDefinitions`, `RenameAchievementDefinition` und
`ChangeAchievementDescription`. Der normale Create-Pfad vergibt die ID selbst, persistiert die
Definition und gibt sie zurück. Get liefert bei unbekannter ID `null`; List liefert immer eine
Liste. Mutationen liefern `true` nur bei einer tatsächlichen Änderung. Eine unbekannte
Definition führt zu `AchievementDefinitionNotFoundException`.

Die Community-Seite bietet `UnlockCommunityAchievement`, `GetCommunityAchievement` und
`ListCommunityAchievements`. Der Unlock erhält nur Community- und Definition-ID. Er validiert
beide Identifier, prüft die Definition im eigenen Katalog, bezieht `IClock.UtcNow`, erzeugt ein
gültiges `CommunityAchievement` und delegiert an den Community-Store. Der Timestamp ist keine
Aufrufer-Eingabe.

Der erste erfolgreiche Write gewinnt dauerhaft. Ein Duplicate Unlock ist ein normaler
idempotenter No-op: Er liefert `false`, verändert keine Zeile und überschreibt insbesondere
nicht den ursprünglichen `UnlockedAtUtc`. Get verwendet den Composite Lookup aus Community- und
Definition-ID. List filtert nach Community und sortiert deterministisch nach
`unlocked_at_utc ASC`, danach `achievement_definition_id ASC`.

## Stores und Persistence

`IAchievementDefinitionStore` bietet Create, Get, List und eine atomare Mutation über einen
synchronen Domain-Callback. `AchievementDefinitionStore` verwendet PostgreSQL und Dapper. Die
Mutation öffnet eine Transaktion, lädt die Definition mit `SELECT ... FOR UPDATE`, rehydriert
die Daten, führt den Callback ohne externe I/O aus und schreibt nur bei tatsächlicher Änderung.
Bei unbekannter ID, Exception oder Cancellation wird zurückgerollt; ein kanonischer No-op führt
zu keinem `UPDATE`.

`ICommunityAchievementStore` bietet ausschließlich Unlock, Get und List. Der
`CommunityAchievementStore` erhält ein bereits gültiges Domainobjekt und führt genau den
atomaren PostgreSQL-Pfad aus:

```sql
INSERT ...
ON CONFLICT (community_identity_id, achievement_definition_id) DO NOTHING
```

Die betroffenen Zeilen bestimmen den Rückgabewert (`1` bedeutet `true`, `0` bedeutet `false`).
Es gibt keine vorgelagerte `SELECT EXISTS`-Abfrage, kein `SELECT FOR UPDATE`, keine künstliche
Root-Zeile und keine unnötige globale Sperre je Community.

## PostgreSQL-Schema und Migration

Die Migration `Achievements:1:CreateAchievementDefinitionsAndCommunityAchievements` legt im
`public`-Schema zuerst `achievement_definitions` und danach `community_achievements` an.

`achievement_definitions` enthält:

- `id uuid primary key`
- `display_name varchar(100) not null`
- `description varchar(500) null`

Checks schützen gegen leere oder ausschließlich aus Whitespace bestehende Anzeigenamen und
Beschreibungen sowie gegen nicht kanonisch getrimmte Werte. Die Zeichenmenge entspricht der
.NET-Whitespace-Semantik einschließlich Unicode-Whitespace und wird deshalb explizit statt nur
über das einfache PostgreSQL-Standard-`btrim()` abgebildet.

`community_achievements` enthält `community_identity_id uuid not null`,
`achievement_definition_id uuid not null` und `unlocked_at_utc timestamptz not null`. Der
Primary Key ist `(community_identity_id, achievement_definition_id)` und deckt den aktuellen
Composite Lookup sowie die Idempotenz ab. Es gibt genau einen internen Foreign Key von
`achievement_definition_id` auf `achievement_definitions(id)`. Einen Foreign Key auf
`community_identities` oder irgendein anderes Modul gibt es ausdrücklich nicht.

## Projektabhängigkeiten und Contracts

`FlurNetz.Modules.Achievements` darf ausschließlich referenzieren:

- `FlurNetz.Modules.Achievements.Contracts`
- `FlurNetz.Modules.Identity.Contracts`
- `FlurNetz.BuildingBlocks`
- `FlurNetz.Persistence`

Es gibt keine Referenz auf `FlurNetz.Messaging`, andere Modulimplementierungen, fremde
Modul-Contracts, API oder Worker. `FlurNetz.Modules.Achievements.Contracts` bleibt vollständig
leer; alle Domain-, Application-, Persistence-, Migration- und Registrierungs-Typen bleiben
implementation-owned.

`AchievementsModule` registriert die beiden Stores, alle acht Use Cases und
`AchievementsMigrationSource`. Es registriert keine eigene `IClock` und überschreibt damit
keine globale Clock-Konfiguration. API- und Worker-Verdrahtung sind nicht enthalten.

## Tests

`FlurNetz.Modules.Achievements.Tests` prüft Identifier, Normalisierung, Unicode-Whitespace,
Grenzwerte, Rehydration, unveränderliche Community-Achievements, UTC-Semantik, Domain-No-ops,
Store-Delegation, Clock-Verwendung, NotFound und die Weitergabe des idempotenten Store-
Ergebnisses.

`FlurNetz.Modules.Achievements.IntegrationTests` verwendet echtes PostgreSQL über Testcontainers
(`postgres:15.1`) oder `FLURNETZ_TEST_CONNECTION_STRING`. Die Tests prüfen Migration und
Idempotenz, Tabellen- und Key-Struktur, direkte Datenbank-Constraints, Katalog-Lifecycle,
Rehydration, Rollback, unbekannte Definitionen, First-successful-write-wins, Get/List,
fehlende Identity-Foreign-Keys und parallele Unlocks desselben sowie verschiedener
Achievements einer Community. Architekturtests sichern zusätzlich Assembly-Referenzen,
Namespace-Grenzen, leere Contracts, Migration-Ownership und Modulregistrierung.

## Bewusst ausgeschlossener Scope

Nicht enthalten sind Achievement Progress, Counter, TargetValue, Rules, Conditions, eine
Generic Rule Engine, Evaluator, Trigger-Konfiguration, Domain Events, Integration Events,
Messaging, Inbox, Outbox, Worker, API, Admin UI, Rewards-, Economy-, Inventory-, Titles-,
Shop-, Notifications-, Overlay- oder sonstige Integrations-Anbindung, Seed-Daten,
Standard-Achievements, Delete, Revoke, Reset, Archive, Enable/Disable, Hidden/Secret,
Localization, Icon, Farbe, Rarity, Category, Points, SortOrder, Slug, TechnicalName und
RewardPackageId.
