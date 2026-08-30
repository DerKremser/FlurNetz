# Inventory-Foundation

## Verantwortung

Das Modul `FlurNetz.Modules.Inventory` besitzt den mengenbasierten Bestand inventarisierbarer
Item-Typen für eine interne `CommunityIdentityId`. Inventory verwendet dafür ausschließlich die
zentrale Identität aus `FlurNetz.Modules.Identity.Contracts`; es führt keine zweite Benutzerkennung
ein und kennt keine externen Plattformidentitäten.

Die Foundation beschreibt nur die fachliche Bestandsposition und ihre Mengeninvarianten. Herkunft,
Belohnungslogik, Preise, Käufe, Shop-Produkte und Darstellung gehören nicht zu Inventory.

## ItemDefinitionId

`ItemDefinitionId` ist ein unveränderlicher, Guid-basierter Fachtyp. Leere GUIDs werden abgelehnt.
Die Kennung identifiziert ausschließlich den fachlichen Typ eines inventarisierbaren Gegenstands.
Ein Item-Katalog mit Name, Beschreibung, Icon, Kategorie, Seltenheit, Status oder weiteren
Metadaten wird in diesem Schritt bewusst nicht vorweggenommen.

Die Definition eines Item-Typs ist von seinem konkreten Community-Bestand getrennt. Damit wird
keine spätere Shop-, Rewards- oder UI-Semantik in die Inventory-Domain gezogen.

## InventoryQuantity

`InventoryQuantity` ist ein unveränderlicher Werttyp auf Basis von `long`. Die Menge ist immer
nicht-negativ; `InventoryQuantity.Zero` ist gültig. `Create(long)` akzeptiert null und positive
Werte und lehnt negative Werte ab.

`Add(long)` und `Remove(long)` akzeptieren ausschließlich positive Änderungen. Eine Addition
oberhalb von `long.MaxValue` wird als `OverflowException` sichtbar abgelehnt. Eine Entnahme,
für die der vorhandene Bestand nicht ausreicht, führt zu
`InsufficientInventoryQuantityException`. Eine Entnahme bis exakt null ist zulässig. Da der
Werttyp immutable ist, verändert eine fehlgeschlagene Operation den ursprünglichen Wert nicht.

## CommunityInventoryEntry

`CommunityInventoryEntry` verbindet genau eine gültige `CommunityIdentityId` mit genau einer
gültigen `ItemDefinitionId`. Diese Kombination beschreibt die fachliche Bestandsposition.
Neue Positionen starten mit `InventoryQuantity.Zero`. Beide Kennungen sind unveränderlich; die
Menge kann nur über die fachlichen Methoden `Add` und `Remove` verändert werden.

Die Foundation trifft noch keine Aussage darüber, ob eine spätere Persistenz Positionen mit
Menge null speichert oder löscht. Diese Lifecycle-Entscheidung gehört in den ersten realen
Persistence-Slice und wird nicht aus der Domain-Foundation vorweggenommen.

## Modulgrenzen und bewusste Ausschlüsse

`FlurNetz.Modules.Inventory.Contracts` bleibt bewusst leer, weil in diesem Schritt noch kein
echter Cross-Module-Caller einen öffentlichen Inventory-Contract benötigt. Die Domain-Typen
bleiben im Implementierungsprojekt. Die einzige fachfremde Referenz der Inventory-Implementierung
ist `FlurNetz.Modules.Identity.Contracts`.

Noch nicht enthalten sind:

- Persistence, SQL-Migrationen, Repository oder Store
- Application Use Cases oder Modulregistrierung
- Messaging, Integration Events, Domain Events, Inbox oder Outbox
- Reward-Definitionen, Reward-Ausführung oder eine Rewards-Abhängigkeit
- Shop-Produkte, Käufe, Preise oder eine Shop-Abhängigkeit
- API, Admin UI oder Worker-Anbindung
- Item-Katalog, Namen, Beschreibungen, Icons, Kategorien oder Seltenheiten
- Stack-Limits, einzigartige Item-Instanzen oder Instanzzustände
- Ausrüstung, Verbrauch, Handel, Transfer, Ablaufzeiten oder Ownership-Historie

Persistence sowie spätere Rewards- und Shop-Komposition werden als getrennte Slices ergänzt,
wenn deren konkrete fachliche Anforderungen vorliegen.
