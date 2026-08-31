# Inventory

## Verantwortung

Das Modul `FlurNetz.Modules.Inventory` besitzt den mengenbasierten Bestand inventarisierbarer
Item-Typen für eine interne `CommunityIdentityId`. Inventory verwendet dafür ausschließlich die
zentrale Identität aus `FlurNetz.Modules.Identity.Contracts`; es führt keine zweite Benutzerkennung
ein und kennt keine externen Plattformidentitäten.

Herkunft, Reward-Ausführung, Preise, Käufe, Shop-Produkte und Darstellung gehören weiterhin nicht
zu Inventory. Der erste persistierte Slice ergänzt ausschließlich den eigenen Bestand und dessen
atomare PostgreSQL-Persistenz.

## Domain

`ItemDefinitionId` ist ein unveränderlicher, Guid-basierter Fachtyp im öffentlichen
`FlurNetz.Modules.Inventory.Contracts`. Leere GUIDs werden abgelehnt. Die Kennung identifiziert
den Typ eines inventarisierbaren Gegenstands. Ein Item-Katalog mit Name, Beschreibung, Icon,
Kategorie, Seltenheit oder Status wird nicht vorweggenommen. Shop verwendet genau diesen Typ und
führt keine zweite Item-ID ein.

`InventoryQuantity` ist ein unveränderlicher Werttyp auf Basis von `long`. Die Menge ist immer
nicht-negativ. `Add(long)` und `Remove(long)` akzeptieren ausschließlich positive Änderungen,
Addition schützt vor Overflow und Entnahme vor Unterbestand.

`CommunityInventoryEntry` verbindet genau eine gültige `CommunityIdentityId` mit genau einer
gültigen `ItemDefinitionId`. `Create` erzeugt eine neue Position bei Menge null; `Rehydrate`
rekonstruiert einen bereits persistierten Zustand. IDs sind unveränderlich und die Menge kann nur
über die fachlichen Methoden `Add` und `Remove` verändert werden.

## Persistenz und Lifecycle

Inventory besitzt die Tabelle `community_inventory_entries` mit genau den Spalten:

- `community_identity_id uuid not null`
- `item_definition_id uuid not null`
- `quantity bigint not null`

Der Composite Primary Key besteht aus
`(community_identity_id, item_definition_id)`. Die Datenbank erzwingt zusätzlich
`quantity >= 0`. Es gibt bewusst keinen Foreign Key auf Identity und keine Item-Definitionstabelle.
Die `CommunityIdentityId` bleibt ein Cross-Module-Identifier; `ItemDefinitionId` bleibt eine
Inventory-eigene Fachkennung ohne vorgezogenen Katalog.

Die Persistenz ist bewusst sparse:

- Ein positiver Bestand wird als Zeile gespeichert.
- `Add` legt eine fehlende Position innerhalb seiner Transaktion zunächst mit Menge null an,
  sperrt sie mit `SELECT FOR UPDATE`, rehydriert die Domain und persistiert erst die fachlich
  gültige positive Menge.
- Ein fehlgeschlagener erster `Add` wird vollständig zurückgerollt und hinterlässt keine Nullzeile.
- `Remove` legt eine fehlende Position nicht an. Fachlich entspricht sie Menge null und eine
  positive Entnahme schlägt mit `InsufficientInventoryQuantityException` fehl.
- Sinkt eine vorhandene Menge exakt auf null, wird die Zeile innerhalb derselben Transaktion
  gelöscht.
- Ein reines Laden erzeugt niemals eine Bestandsposition.

Diese Lifecycle-Regel verhindert dauerhaft bedeutungslose Nullbestände und macht das Nichtvorhandensein
einer Zeile zur persistenten Repräsentation von Bestand null.

## Application und Persistence-Grenze

`ICommunityInventoryStore` ist ein rein modulinterner Port. Er bietet atomare Operationen für
`Add`, `Remove` und das Laden genau einer Position. `AddInventoryQuantity` und
`RemoveInventoryQuantity` enthalten keine SQL- oder Transaktionslogik und delegieren an diese
Persistenzgrenze.

`CommunityInventoryStore` implementiert den Port mit Dapper und der technischen
`FlurNetz.Persistence`-Foundation. Jede Mutation verwendet eine eigene
`PostgreSqlTransaction`; Lesen, Zeilensperre, Rehydration, Domain-Mutation und Update beziehungsweise
Delete liegen in derselben Transaktion. Dadurch werden Lost Updates bei parallelen Änderungen an
derselben Bestandsposition verhindert.

Es gibt weiterhin bewusst keinen transaction-aware öffentlichen Inventory-Contract und keinen
Overload für fremde Modultransaktionen. `ItemDefinitionId` ist ausschließlich der gemeinsame
fachliche Identifier; eine Grant-Capability wird erst eingeführt, wenn ein späterer Slice sie
tatsächlich benötigt.

## Migration und Registrierung

`Inventory:1:CreateCommunityInventoryEntries` gehört ausschließlich dem Inventory-Modul und legt
nur die Inventory-eigene Tabelle an. Die Migration enthält keine Cross-Module-Foreign-Keys.

`AddInventoryModule` registriert den Store, die beiden internen Use Cases und
`InventoryMigrationSource`. Kein Host verdrahtet Inventory in diesem Slice; API, Worker und
Runtime-Trigger bleiben unberührt.

## Contracts und bewusste Ausschlüsse

`FlurNetz.Modules.Inventory.Contracts` enthält in diesem Slice ausschließlich
`ItemDefinitionId`. Bestandsoperationen, Stores, Domainobjekte und Persistence bleiben intern.
Die Inventory-Implementierung darf ausschließlich den eigenen Contract, `Identity.Contracts` und
die technische `FlurNetz.Persistence`-Assembly referenzieren.

Weiterhin nicht enthalten sind:

- Messaging, Integration Events, Domain Events, Inbox oder Outbox
- Reward-Definitionen, Reward-Ausführung oder eine Rewards-Abhängigkeit
- Shop-Produkte, Käufe, Preise oder eine Shop-Abhängigkeit
- API, Admin UI oder Worker-Anbindung
- Item-Katalog, Namen, Beschreibungen, Icons, Kategorien oder Seltenheiten
- Stack-Limits, einzigartige Item-Instanzen oder Instanzzustände
- Ausrüstung, Verbrauch, Handel, Transfer, Ablaufzeiten oder Ownership-Historie
- öffentliche transaction-aware Inventory-Capabilities oder Inventory-Grant-Funktionen

## Tests

Die Unit Tests prüfen Domain-Rehydration, Mengeninvarianten, Immutability und die beiden internen
Use Cases. Das eigene PostgreSQL-Integrationstestprojekt prüft Migration und Check-Constraint,
Composite Key, Lazy-Add, Sparse-Zero-Lifecycle, Laden, Rollback, Isolation verschiedener
Identitäten und Item-Definitionen sowie konkurrierende Adds und Removes mit echten
PostgreSQL-Zeilensperren. Architekturtests sichern Assembly-Referenzen, Typ-Ownership, die
minimale Contracts-Assembly und die verbotenen Messaging-, Rewards- und Shop-Abhängigkeiten ab.
