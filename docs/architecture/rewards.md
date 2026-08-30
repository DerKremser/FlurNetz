# Rewards-Vertical-Slice

## Verantwortung

Das Rewards-Modul besitzt die fachliche Bedeutung konfigurierter Belohnungen. Es
persistiert Reward Definitions und Reward Packages, beschreibt die fachliche Herkunft eines
Grants und führt pro ausgeführter Definition einen `RewardGrant`-Record. Rewards besitzt
dabei nicht den resultierenden Zustand anderer Module. Eine Economy-Balance wird deshalb
nicht von Rewards modelliert oder direkt verändert; Rewards beschreibt nur die gewünschte
Wirkung und delegiert den tatsächlichen Write an die öffentliche Economy-Fähigkeit.

Der erste und derzeit einzige ausführbare Zieltyp ist
`EconomyBalanceRewardDefinition`. Der persistierte Inventory-Slice existiert inzwischen unabhängig
von Rewards und veröffentlicht weiterhin keinen Cross-Module-Contract; eine Inventory-Reward-
Definition sowie Title-Rewards werden erst in eigenen späteren Slices ergänzt. XP bleiben vollständig Progression-owned und sind kein Reward-Komponententyp.

## Reward Definitions

`RewardDefinition` ist die minimale abstrakte Basis und besitzt ausschließlich ihre
`RewardDefinitionId`. `RewardDefinitionId`, `RewardPackageId` und `RewardGrantId` sind
getrennte, unveränderliche, Guid-basierte Fachtypen; leere GUIDs werden abgelehnt. Es gibt
keine gemeinsame generische ID-Infrastruktur und keine UI-Metadaten wie Name, Beschreibung,
Icon, Kategorie oder Status.

`EconomyBalanceRewardDefinition` beschreibt eine positive Economy-Balance-Gutschrift über
einen neutralen `long Amount`. Die Definition trägt keine Währungsbezeichnung, keinen
Economy-Zustand und keine Overflow-Prüfung des Zielbestands. Die Ziel-Domain Economy bleibt
Autorität für die tatsächliche Gutschrift und ihre bestehenden Invarianten.

## Persistierter Katalog

`CreateEconomyBalanceRewardDefinition` erzeugt eine neue Definitions-ID, validiert die
fachliche Definition und persistiert sie. `CreateRewardPackage` verwendet
`RewardPackage.Create`, prüft alle Definitionen auf Existenz und schreibt Package sowie
Membership atomar. Unbekannte Definitionen führen zu
`RewardDefinitionNotFoundException`; es bleibt dabei keine Package-Zeile zurück.

`RewardPackage` fasst mindestens eine gültige, doppelfreie `RewardDefinitionId` zusammen.
Die technische Collection bleibt deterministisch, aber ihre Reihenfolge ist keine fachliche
Ausführungszusage. Ein Package beschreibt eine verpflichtende Menge: Bei der V1-Ausführung
müssen alle Komponenten erfolgreich sein oder keine. Optional-, Choice-, Random- und
Weighted-Semantik existieren nicht.

Die eigene Migration `Rewards:1:CreateRewardConfigurationAndGrants` gehört dem Rewards-Modul.
Sie legt `reward_definitions`, `reward_packages`, `reward_package_definitions` und
`reward_grants` mit den erforderlichen Primärschlüsseln und internen Foreign Keys an.
`reward_definitions` verwendet für den ersten Typ den stabilen Code `economy_balance` und
`amount bigint` mit `CHECK (amount > 0)`. Es gibt keinen JSON-Konfigurationsblob.

## RewardSource und Grant-Eindeutigkeit

`RewardSource` beschreibt die fachliche Ursache eines Grants mit nicht leeren,
nicht aus Whitespace bestehenden Strings `SourceType` und `SourceId`. `SourceType` ist
bewusst kein Enum: Neue stabile Quelltypen aus anderen Domänen können so hinzukommen, ohne
das Rewards-Domainmodell zu ändern. Event-, Message- und Inbox-IDs gehören nicht in diese
fachliche Quelle.

`RewardGrant` ist der Record genau einer ausgeführten `RewardDefinitionId`. Er verwendet die
zentrale `CommunityIdentityId` als Empfängeridentität, enthält aber weder eine zweite
Benutzer-ID noch eine `RewardPackageId`, einen Status oder Zeitstempel.

Die technische und fachliche Idempotency-Grenze lautet exakt:

```text
SourceType + SourceId + RewardDefinitionId
```

`CommunityIdentityId` ist ausdrücklich nicht Teil dieses Schlüssels. Eine fachliche Quelle
bezeichnet einen konkreten Vorgang und muss deshalb so gewählt werden, dass ihre `SourceId`
diesen Vorgang eindeutig identifiziert. Die Datenbank-Unique-Constraint ist die
authoritative Concurrency-Grenze.

## Atomare Ausführung

`GrantRewardPackage` enthält keine SQL- oder Transaktionslogik. Der gezielte Port
`IRewardPackageGrantExecutor` delegiert an den PostgreSQL-Executor. Dieser lädt Package und
Definitionen, sortiert sie technisch stabil nach `RewardDefinitionId`, reserviert die
`RewardGrant`-Zeilen mit `INSERT ... ON CONFLICT DO NOTHING` und bewertet anschließend den
Zustand.

Werden alle Definitionen neu reserviert, ruft der Executor für jede aktuell unterstützte
`EconomyBalanceRewardDefinition` `IEconomyBalanceCredit` auf. Package-Grants und Economy-
Writes teilen exakt dieselbe `DbConnection` und `DbTransaction`. Die Grant-Zeilen dürfen vor
den Effects innerhalb dieser noch nicht bestätigten Transaktion bestehen; erst der
gemeinsame Commit macht den vollständigen Grant sichtbar. Jeder Fehler rollt alle Grant-
Reservierungen und alle Economy-Writes gemeinsam zurück.

Sind alle Definitionen bereits vorhanden, ist der erneute Grant ein idempotenter No-op und
liefert `AlreadyGranted`. Es entsteht weder ein zweiter Grant-Record noch ein zweiter
Economy-Effekt. Wenn nur ein Teil der Package-Definitionen bereits vorhanden ist, wird der
inkonsistente Partial-Grant-Zustand als Fehler abgelehnt; der fehlende Rest wird nicht still
ausgeführt. Die deterministische technische Reihenfolge verspricht keine fachliche
Reihenfolge, reduziert aber unnötige Deadlock-Risiken bei parallelen Aufrufen.

Persistierte unbekannte Definitionstypen werden klar abgelehnt und nicht ignoriert. Ein
Package mit mehreren Economy-Definitionen ist daher vollständig All-or-Nothing.

## Modulgrenzen und bewusste Ausschlüsse

Rewards referenziert nur `FlurNetz.Modules.Rewards.Contracts`,
`FlurNetz.Modules.Identity.Contracts`, `FlurNetz.Modules.Economy.Contracts` und
`FlurNetz.Persistence`. Die konkrete Economy-Implementierung wird nicht referenziert;
Economy kennt Rewards ebenfalls nicht. Rewards-Tabellen besitzen keinen Foreign Key auf
`community_identities` oder `community_economies`. Die atomare Zusammenarbeit erfolgt über
den öffentlichen Economy-Contract und die gemeinsame PostgreSQL-Transaktion.

`FlurNetz.Modules.Rewards.Contracts` bleibt bewusst leer, weil noch kein anderer Modul-Caller
einen Rewards-Contract benötigt. Die Domain- und Application-Typen bleiben im
Implementierungsprojekt.

Noch nicht enthalten sind:

- XP-Reward-Typen oder eine Progression-Abhängigkeit
- Inventory- oder Title-Definitionen
- Achievements, Daily- oder andere Runtime-Trigger
- weitere Währungsbezeichnungen, Multi-Currency, Ledger oder Transfers
- generische Reward-Engine-, Pipeline-, Executor- oder Repository-Infrastruktur
- Messaging, Integration Events, Domain Events, Inbox oder Outbox
- zusätzliche Application Use Cases für Lesen, Ändern oder Löschen
- API, Admin UI oder Worker-Anbindung
