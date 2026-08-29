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

## Contracts und Implementierung

Die Contracts-Assembly beschreibt ausschließlich die später öffentliche Modulgrenze. Sie bleibt in diesem Schritt bewusst leer und enthält keine vorsorglichen DTOs, Commands, Queries, Services, Repositories, Entities, Value Objects oder Events.

Die Implementierungs-Assembly ist der spätere Ort für Domain, Application, interne Persistence-Adapter, interne Event Handler und die Modulregistrierung. Auch sie bleibt in diesem Schritt fachlich leer und referenziert nur das eigene Contracts-Projekt.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Cross-Module-Kommunikation erfolgt später ausschließlich über freigegebene öffentliche Contracts und Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine vorsorglichen Shared-Entities.

Die modulbezogenen Testprojekte sind zunächst ebenfalls technisch minimal. Fachliche Tests kommen mit den jeweiligen Vertical Slices hinzu. Die Architecture Tests prüfen die Assembly-, Referenz- und Namespace-Grenzen automatisiert.

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

Diese Reihenfolge dokumentiert nur die spätere Umsetzung. In diesem Schritt wird keine Fachlogik begonnen. Identity wird später als erstes Referenzmodul mit einer minimalen internen Community Identity implementiert.

## Cross-Cutting-Fähigkeiten

Audit und Analytics werden in diesem Schritt nicht als eigene Assemblies angelegt. Sie werden erst eingeführt, wenn reale fachliche Aktionen und Events einen konkreten Bedarf dafür erzeugen.
