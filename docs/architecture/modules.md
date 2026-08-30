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

Die Engagement-Foundation ist vorhanden. `FlurNetz.Modules.Engagement` enthält die minimale
`EngagementActivity` mit einer modulinternen `EngagementActivityId` und der direkt verwendeten
`CommunityIdentityId` aus `FlurNetz.Modules.Identity.Contracts`.

`FlurNetz.Modules.Engagement.Contracts` bleibt bewusst leer. Es gibt noch keinen Recording-
Use-Case, keine Activity Types, keine Persistenz, keine Events, keine Progression-Kommunikation,
keine API-Erweiterung und keine Plattformintegration.

## Contracts und Implementierung

Die Contracts-Assemblies beschreiben die später öffentliche Modulgrenze. Die Contracts-Assemblies
der übrigen Module bleiben in diesem Schritt bewusst leer und enthalten keine vorsorglichen DTOs,
Commands, Queries, Services, Repositories, Entities, Value Objects oder Events. Identity bildet
mit `CommunityIdentityId` die bewusst minimale Foundation-Ausnahme; Engagement besitzt zwar
bereits seine Domain-Foundation, benötigt aber noch keinen öffentlichen Contract.

Die Implementierungs-Assembly ist der Ort für Domain, Application, interne
Persistence-Adapter, interne Event Handler und die Modulregistrierung. Identity nutzt davon
aktuell nur Domain, Application, den Persistenzadapter, die fachliche Migration und die
Registrierung der tatsächlich vorhandenen Komponenten. Die übrigen Implementierungs-Assemblies
bleiben fachlich leer.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Engagement
darf in diesem Foundation-Schritt ausschließlich den eigenen Contract und `Identity.Contracts`
verwenden. Cross-Module-Kommunikation erfolgt später ausschließlich über freigegebene öffentliche
Contracts und Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine vorsorglichen
Shared-Entities.

Die modulbezogenen Testprojekte bleiben für die übrigen Module technisch minimal. Die Identity-
Unit- und PostgreSQL-Integrationstests prüfen Domain, Use Case, Migration, Commit/Rollback,
Primärschlüssel und Laden. Die Architecture Tests prüfen die Assembly-, Referenz- und
Namespace-Grenzen automatisiert.

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
