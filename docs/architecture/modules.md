# Fachmodule

FlurNetz bildet die vorgesehenen Fachmodule zunächst als physische Assembly-Grenzen ab. Jedes Modul besteht aus einer öffentlichen Contracts-Assembly und einer separaten Implementierungs-Assembly:

| Modul | Contracts | Implementierung |
| --- | --- | --- |
| Identity | `FlurNetz.Modules.Identity.Contracts` | `FlurNetz.Modules.Identity` |
| Engagement | `FlurNetz.Modules.Engagement.Contracts` | `FlurNetz.Modules.Engagement` |
| Progression | `FlurNetz.Modules.Progression.Contracts` | `FlurNetz.Modules.Progression` |
| Economy | `FlurNetz.Modules.Economy.Contracts` | `FlurNetz.Modules.Economy` |
| Rewards | `FlurNetz.Modules.Rewards.Contracts` | `FlurNetz.Modules.Rewards` |
| Inventory | `FlurNetz.Modules.Inventory.Contracts` | `FlurNetz.Modules.Inventory` |
| Titles | `FlurNetz.Modules.Titles.Contracts` | `FlurNetz.Modules.Titles` |
| Achievements | `FlurNetz.Modules.Achievements.Contracts` | `FlurNetz.Modules.Achievements` |
| Shop | `FlurNetz.Modules.Shop.Contracts` | `FlurNetz.Modules.Shop` |
| Notifications | `FlurNetz.Modules.Notifications.Contracts` | `FlurNetz.Modules.Notifications` |
| Automation | `FlurNetz.Modules.Automation.Contracts` | `FlurNetz.Modules.Automation` |
| Overlay | `FlurNetz.Modules.Overlay.Contracts` | `FlurNetz.Modules.Overlay` |
| Integrations | `FlurNetz.Modules.Integrations.Contracts` | `FlurNetz.Modules.Integrations` |
| Administration | `FlurNetz.Modules.Administration.Contracts` | `FlurNetz.Modules.Administration` |

## Aktueller Stand des Identity-Moduls

Identity ist das erste Modul mit einem vollständigen, bewusst kleinen fachlichen Vertical Slice.
`FlurNetz.Modules.Identity.Contracts` enthält ausschließlich den stabilen internen Identifier
`CommunityIdentityId`. Die Implementierungs-Assembly enthält die minimale `CommunityIdentity`,
den `CreateCommunityIdentity`-Use-Case, einen moduleigenen Persistenz-Port, den Dapper-/Npgsql-
Adapter und die Identity-eigene Migration `Identity:1:CreateCommunityIdentities`.

Der Slice kann eine neue interne Identität erzeugen, in PostgreSQL speichern und über ihre ID
wieder laden. Die fachliche Tabelle enthält ausschließlich den UUID-Primärschlüssel `id`.
Migration und Persistenz werden durch echte PostgreSQL-Integrationstests geprüft.

Der vorhandene Use Case ist jetzt über `FlurNetz.Api` als HTTP-Adapter erreichbar. Weiterhin
nicht enthalten sind weitere Identity-Use-Cases, Plattformkonten, Authentifizierung, Profile
sowie fachliche Domain- oder Integration Events.

## Aktueller Stand des Engagement-Moduls

Der erste vollständige Engagement-Recording-Vertical-Slice ist vorhanden. `RecordMessageEngagement`
erzeugt eine normalisierte Message-Aktivität mit `EngagementActivityId`, der direkt verwendeten
`CommunityIdentityId` aus `FlurNetz.Modules.Identity.Contracts` und einem UTC-Zeitpunkt aus
`IClock`. Die Aktivität wird über den internen Repository-Port und den Dapper/PostgreSQL-Adapter
gespeichert; die Migration `Engagement:1:CreateEngagementActivities` gehört dem Engagement-Modul.

`FlurNetz.Modules.Engagement.Contracts` bleibt bewusst leer. Engagement ist damit noch nicht
als vollständiges Engagement-Modul ausgebaut: Es gibt ausschließlich den Activity Type `Message`,
keinen Nachrichtentext, keine Plattformdaten, keine Events, keine Progression-Kommunikation und
keine API-Erweiterung.

## Aktueller Stand des Progression-Moduls

Progression besitzt die minimale Domain-Foundation für den fachlichen Fortschritt einer
Community-Identität. `ExperiencePoints` modelliert nicht-negative, auf `long` basierende XP
mit sicherer Addition; `CommunityProgression` ordnet den Wert einer bestehenden
`CommunityIdentityId` zu und startet mit `0` XP. Positive XP können akkumuliert werden.

`FlurNetz.Modules.Progression.Contracts` bleibt bewusst leer. Es gibt noch keinen persistierten
Vertical Slice und keine Level-Logik, Persistence, Messaging- oder Engagement-Kommunikation,
Events, Rewards oder API-Erweiterung. Die einzige fachfremde Projektabhängigkeit der
Implementierung ist `FlurNetz.Modules.Identity.Contracts`.

## Contracts und Implementierung

Die Contracts-Assemblies beschreiben die später öffentliche Modulgrenze. Die Contracts-Assemblies
der übrigen Module bleiben in diesem Schritt bewusst leer und enthalten keine vorsorglichen DTOs,
Commands, Queries, Services, Repositories, Entities, Value Objects oder Events. Identity bildet
mit `CommunityIdentityId` die bewusst minimale Foundation-Ausnahme; Engagement besitzt zwar
bereits seine Domain-Foundation, benötigt aber noch keinen öffentlichen Contract. Progression
besitzt ebenfalls eine interne Domain-Foundation, benötigt in diesem Schritt aber noch keinen
öffentlichen Contract.

Die Implementierungs-Assembly ist der Ort für Domain, Application, interne
Persistence-Adapter, interne Event Handler und die Modulregistrierung. Identity nutzt davon
aktuell nur Domain, Application, den Persistenzadapter, die fachliche Migration und die
Registrierung der tatsächlich vorhandenen Komponenten. Engagement nutzt dieselben Schichten
für seinen Message-Recording-Slice und registriert Use Case, Repository, Migration und Uhr.
Die übrigen Implementierungs-Assemblies bleiben fachlich leer.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Engagement
darf ausschließlich den eigenen Contract, `Identity.Contracts` sowie die ausdrücklich erlaubten
technischen BuildingBlocks- und Persistence-Projekte verwenden. Cross-Module-Kommunikation erfolgt später ausschließlich über freigegebene öffentliche
Contracts und Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine vorsorglichen
Shared-Entities.

Die modulbezogenen Testprojekte bleiben für die übrigen Module technisch minimal. Die Identity-
und Engagement-Unit- sowie PostgreSQL-Integrationstests prüfen jeweils die vorhandenen Domain-
und Use-Case-Flows, Migration, Commit/Rollback, Primärschlüssel und Laden. Die Architecture
Tests prüfen die Assembly-, Referenz- und Namespace-Grenzen automatisiert; Progression wird in
diesem Foundation-Schritt durch fokussierte Domain- und Architecture-Tests abgesichert.

## Verbindliche spätere Implementierungsreihenfolge

1. Identity
2. Engagement
3. Progression
4. Economy
5. Rewards
6. Inventory
7. Titles
8. Achievements
9. Shop
10. Notifications
11. Automation
12. Overlay
13. Integrations
14. Administration

Diese Reihenfolge dokumentiert die Umsetzung. Identity ist als erstes Referenzmodul mit einem
minimalen Vertical Slice umgesetzt; weitere fachliche Identity-Funktionalität folgt erst mit
konkretem Bedarf.

## Cross-Cutting-Fähigkeiten

Audit und Analytics werden in diesem Schritt nicht als eigene Assemblies angelegt. Sie werden erst eingeführt, wenn reale fachliche Aktionen und Events einen konkreten Bedarf dafür erzeugen.
