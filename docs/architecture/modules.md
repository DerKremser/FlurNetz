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
`IClock`. Die Aktivität wird gemeinsam mit einem `MessageEngagementRecordedIntegrationEvent`
über den atomaren Recorder und den bestehenden PostgreSQL-Outbox-Publisher persistiert; die
Migration `Engagement:1:CreateEngagementActivities` gehört dem Engagement-Modul.

`FlurNetz.Modules.Engagement.Contracts` enthält als ersten öffentlichen Vertrag das Event mit
dem stabilen Message Type `engagement.message-recorded` und Schema-Version `1`. Die Payload
enthält nur die interne `CommunityIdentityId`; XP, Message-Text, Plattformdaten und
Progressionsanweisungen sind ausgeschlossen. Engagement ruft Progression nicht direkt auf.

## Aktueller Stand des Progression-Moduls

Progression besitzt den ersten persistierten Vertical Slice für den fachlichen Fortschritt
einer Community-Identität. `ExperiencePoints` modelliert nicht-negative, auf `long`
basierende XP mit sicherer Addition; `CommunityProgression` ordnet den Wert einer bestehenden
`CommunityIdentityId` zu und startet bei `0` XP. `GrantExperience` erzeugt den Zustand lazy
bei der ersten Vergabe und persistiert die positive XP-Akkumulation atomar in PostgreSQL.

Die Persistence-Mutation verwendet eine modulinterne Port-/Adapter-Grenze, eine gemeinsame
Transaktion und `SELECT FOR UPDATE`, damit parallele Vergaben keine Lost Updates erzeugen.
Die Tabelle verwendet `CommunityIdentityId` als Primärschlüssel und besitzt bewusst keinen
Foreign Key auf Identity. `FlurNetz.Modules.Progression.Contracts` bleibt leer.
Die fachfremde fachliche Projektabhängigkeit der Implementierung ist
`FlurNetz.Modules.Engagement.Contracts`; zusätzlich verwendet sie `Identity.Contracts`,
`FlurNetz.Persistence` und die technische Messaging-Foundation. Eine Referenz auf die
Engagement-Implementierung existiert nicht.

Progression konsumiert das Engagement-Event über den stabilen Consumer
`progression.message-engagement-xp`. Eine normalisierte Message wird ausschließlich in
Progression als `1 XP` interpretiert; Inbox-Eintrag und transaction-aware XP-Grant teilen eine
Transaktion. Der Consumer wird durch `FlurNetz.Worker` kontinuierlich ausgeführt. Noch nicht
enthalten sind Level-Logik, Rewards und eine API-Erweiterung.

## Aktueller Stand des Economy-Moduls

Economy besitzt jetzt einen kleinen persistierten Vertical Slice für den community-bezogenen
Economy-Zustand einer internen `CommunityIdentityId`. `EconomyBalance` ist ein unveränderlicher,
auf `long` basierender und nicht-negativer Wert. Positive Gutschriften akkumulieren sicher bis
`long.MaxValue`; ein Overflow wird sichtbar abgelehnt. Positive Abbuchungen dürfen den Saldo
nicht überziehen und können ihn exakt auf null reduzieren.

`CommunityEconomy` enthält ausschließlich `CommunityIdentityId` und `Balance`, startet
bei null und verändert den Saldo nur über `Credit` und `Debit`. `Create` und `Rehydrate` sind
getrennte Domain-Wege. Der interne `ICommunityEconomyStore` bietet Credit, Debit, Load und
einen transaction-aware Credit-Overload ohne eigenen Commit. Die Use Cases
`CreditEconomyBalance` und `DebitEconomyBalance` enthalten keine SQL- oder
Transaktionslogik. Der PostgreSQL-Adapter rehydriert den Zustand und führt jede Mutation als
atomare Read/Modify/Write-Transaktion mit `SELECT FOR UPDATE` aus. Credits legen einen fehlenden
Zustand erst bei Erfolg lazy an; ein Debit auf einen fehlenden Zustand behandelt den Saldo als
null, wirft bei positivem Betrag `InsufficientEconomyBalanceException` und legt keine Zeile an.
`FlurNetz.Modules.Economy.Contracts` enthält nun ausschließlich die neutrale Fähigkeit
`IEconomyBalanceCredit` mit `CommunityIdentityId`, `long`, `DbConnection` und `DbTransaction`.
Sie ermöglicht eine gemeinsame Transaktion mit einem aufrufenden Slice; Economy kennt Rewards
dabei nicht. Eine konkrete Währungsbezeichnung und eine Multi-Currency-Struktur werden bewusst
nicht vorweggenommen; Events, Messaging, Transfers, Rewards-Trigger, Shop-Funktionalität und
API gehören weiterhin nicht zum Slice.

## Aktueller Stand des Rewards-Moduls

Rewards besitzt nun einen ersten persistierten und ausführbaren Vertical Slice. `RewardDefinitionId`,
`RewardPackageId` und `RewardGrantId` sind getrennte, unveränderliche Guid-basierte Fachtypen.
`RewardDefinition` trägt ausschließlich ihre Kennung; der erste und einzige ausführbare
Definitionstyp `EconomyBalanceRewardDefinition` beschreibt eine positive Economy-Balance-
Gutschrift mit einem neutralen `long Amount`, ohne Economy zu referenzieren oder den Economy-
Zustand zu besitzen.

`RewardPackage` fasst mindestens eine gültige, doppelfreie Definition zusammen und beschreibt
eine verpflichtende Menge: Bei der Ausführung müssen entweder alle Komponenten erfolgreich
sein oder keine. `RewardSource` bildet die nicht leere Herkunft aus `SourceType` und `SourceId`
ab; `SourceType` ist bewusst kein Enum. `RewardGrant` ist einem Empfänger über die zentrale
`CommunityIdentityId` und genau einer `RewardDefinitionId` zugeordnet. Die Eindeutigkeit
`SourceType + SourceId + RewardDefinitionId` wird in `reward_grants` technisch erzwungen;
Duplicates sind idempotente No-ops, Partial-State ist ein Fehler.

Die Migration `Rewards:1:CreateRewardConfigurationAndGrants` und der PostgreSQL-Executor
liegen im Rewards-Modul. Economy wird über den schmalen öffentlichen Capability-Contract in
derselben Transaktion gutgeschrieben. `FlurNetz.Modules.Rewards.Contracts` bleibt leer.
XP bleiben Progression-owned; Inventory- und Title-Rewards werden erst nach der Existenz der
jeweiligen Zielmodule modelliert. Es gibt noch keinen Runtime-Trigger, keine API und keine
Worker-Anbindung.

## Contracts und Implementierung

Die Contracts-Assemblies beschreiben die später öffentliche Modulgrenze. Die Contracts-Assemblies
der übrigen noch nicht fachlich begonnenen Module bleiben in diesem Schritt bewusst leer und enthalten keine vorsorglichen DTOs,
Commands, Queries, Services, Repositories, Entities, Value Objects oder Events. Identity bildet
mit `CommunityIdentityId` die bewusst minimale Foundation-Ausnahme; Engagement besitzt bereits
seine Domain-Foundation und mit dem Message-Event den ersten öffentlichen Contract. Progression
besitzt mit Domain, Application, Persistence-Adapter, Migration, Consumer und Registrierung
einen internen Vertical Slice, benötigt aber weiterhin keinen öffentlichen Contract. Economy besitzt
mit Domain, Application, Persistence-Adapter, Migration und Registrierung ebenfalls einen internen
Vertical Slice sowie den neutralen Credit-Capability-Contract für atomare Komposition. Rewards besitzt
mit Domain, Application, Katalog, Grant-Executor, Migration und Registrierung den ersten persistierten
ausführbaren Rewards-Slice; sein eigenes Contracts-Projekt bleibt leer.

Die Implementierungs-Assembly ist der Ort für Domain, Application, interne
Persistence-Adapter, interne Event Handler und die Modulregistrierung. Identity nutzt davon
aktuell nur Domain, Application, den Persistenzadapter, die fachliche Migration und die
Registrierung der tatsächlich vorhandenen Komponenten. Engagement nutzt dieselben Schichten
für seinen Message-Recording-Slice und registriert Use Case, Repository, Migration und Uhr.
Progression nutzt Domain, Application, einen atomaren Store, Migration, Consumer und Registrierung;
der unabhängige Worker-Host verdrahtet diesen Slice für die Runtime. Economy nutzt Domain,
Application, einen atomaren Store, Migration und Registrierung; kein Host verdrahtet den Slice
und es gibt keine öffentliche API. Rewards nutzt Domain, Application, gezielte Katalog- und
Grant-Persistence, Migration und Registrierung; kein Host verdrahtet den Slice und es gibt
keine öffentliche API. Die übrigen
Implementierungs-Assemblies bleiben fachlich leer.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Engagement
darf den eigenen Contract, `Identity.Contracts` sowie die ausdrücklich erlaubten technischen
BuildingBlocks-, Persistence- und Messaging-Projekte verwenden. Progression darf zusätzlich
ausschließlich `Engagement.Contracts` und Messaging verwenden; die Engagement-Implementierung
bleibt verboten. Economy darf `Identity.Contracts` und seinen eigenen öffentlichen
Capability-Contract verwenden; Rewards darf zusätzlich `Identity.Contracts` und
`Economy.Contracts` verwenden und referenziert keine Economy-Implementierung.
Cross-Module-Kommunikation erfolgt über freigegebene öffentliche Contracts
und Integration Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine
vorsorglichen Shared-Entities.

Die modulbezogenen Testprojekte bleiben für die übrigen Module technisch minimal. Die Identity-
und Engagement-Unit- sowie PostgreSQL-Integrationstests prüfen jeweils die vorhandenen Domain-
und Use-Case-Flows, Migration, Commit/Rollback, Primärschlüssel und Laden. Progression wird
zusätzlich mit Domain-, Use-Case-, Migration-, Rollback-, Load- und echten PostgreSQL-
Concurrency-Tests abgesichert. Economy besitzt eigene Domain-, Use-Case-, Migration-, Lifecycle-,
Rollback-, Load- und echte PostgreSQL-Concurrency-Tests. Rewards besitzt Domain- und
Application-Unit-Tests, Architekturtests sowie ein eigenes echtes PostgreSQL-
Integrationstestprojekt für Migration, Katalog, Atomicity, Idempotenz und Nebenläufigkeit. Das separate `FlurNetz.Workflows.IntegrationTests`-Projekt prüft
den vollständigen Outbox-/Inbox-Weg sowie Producer- und Consumer-Atomicity gegen PostgreSQL.
Die Architecture Tests prüfen zusätzlich Event Ownership, Contract-Minimalität, erlaubte
Messaging-Kanten, die Rewards-Abhängigkeitsgrenze und die Consumer-Grenzen automatisiert.

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
