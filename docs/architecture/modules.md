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

Identity ist das erste Modul mit einer fachlichen Foundation. `FlurNetz.Modules.Identity.Contracts`
enthält ausschließlich den stabilen internen Identifier `CommunityIdentityId`. Die
Implementierungs-Assembly enthält unter `Domain/` die minimale `CommunityIdentity`, die nur
diese unveränderliche ID trägt.

Damit ist noch kein vollständiger Identity-Use-Case implementiert. Insbesondere gibt es keine
fachliche Persistenz, Migration, Repositories, API, Plattformkonten, Authentifizierung oder
fachlichen Events.

## Contracts und Implementierung

Die Contracts-Assemblies beschreiben die später öffentliche Modulgrenze. Die Contracts-Assemblies
der übrigen Module bleiben in diesem Schritt bewusst leer und enthalten keine vorsorglichen DTOs,
Commands, Queries, Services, Repositories, Entities, Value Objects oder Events. Identity bildet
mit `CommunityIdentityId` die bewusst minimale Foundation-Ausnahme und enthält darüber hinaus
keine vorsorglichen Verträge.

Die Implementierungs-Assembly ist der spätere Ort für Domain, Application, interne
Persistence-Adapter, interne Event Handler und die Modulregistrierung. Identity enthält bereits
die minimale Domain-Identität `CommunityIdentity` und referenziert nur das eigene
Contracts-Projekt. Die übrigen Implementierungs-Assemblies bleiben in diesem Schritt fachlich
leer.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Cross-Module-Kommunikation erfolgt später ausschließlich über freigegebene öffentliche Contracts und Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine vorsorglichen Shared-Entities.

Die modulbezogenen Testprojekte sind zunächst technisch minimal. Das Identity-Testprojekt prüft
die Invarianten der Foundation; weitere fachliche Tests kommen mit den jeweiligen Vertical Slices
hinzu. Die Architecture Tests prüfen die Assembly-, Referenz- und Namespace-Grenzen automatisiert.

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

Diese Reihenfolge dokumentiert die Umsetzung. Identity besitzt als erstes Referenzmodul bereits
eine minimale interne Community Identity; der vollständige Identity-Use-Case folgt in einem
separaten Vertical Slice.

## Cross-Cutting-Fähigkeiten

Audit und Analytics werden in diesem Schritt nicht als eigene Assemblies angelegt. Sie werden erst eingeführt, wenn reale fachliche Aktionen und Events einen konkreten Bedarf dafür erzeugen.
