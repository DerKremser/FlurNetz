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

Identity besitzt weiterhin den minimalen persistierten Vertical Slice für die zentrale
interne `CommunityIdentityId`. `CommunityIdentity`, `CreateCommunityIdentity`, der
moduleigene Repository-Port und `Identity:1:CreateCommunityIdentities` bleiben unverändert.

`Identity.Contracts` enthält jetzt genau zwei öffentliche Typen:
`CommunityIdentityId` und die caller-neutrale `ICommunityIdentityExistence`-Capability.
Die Capability prüft die Existenz einer internen Identität innerhalb einer vom aufrufenden
Slice bereitgestellten `DbConnection`/`DbTransaction` und führt keinen Commit aus.
Der Shop-Purchase ist der erste Aufrufer. Identity kennt Shop nicht und veröffentlicht weder
Repository noch Domainobjekt oder SQL-Persistenz.

Der bestehende Create-Use-Case bleibt über `FlurNetz.Api` erreichbar. Plattformkonten,
Authentifizierung, Profile sowie Identity-eigene Domain- oder Integration Events sind weiterhin
nicht enthalten. Details stehen in [identity.md](identity.md).

## Aktueller Stand des Integrations-Moduls

Integrations V1 ist der erste vollständige External-Identity-Mapping- und
Resolution-Slice. IntegrationProviderKey und ExternalUserId sind kleine, validierte
Contract-Value-Types; die Kombination aus Provider-Key und opaque externer User-ID wird
in ExternalIdentityMapping genau einer CommunityIdentityId zugeordnet.

LinkExternalIdentity prüft die Zielidentität über ICommunityIdentityExistence in der
Mapping-Transaktion. Identisches Linken ist idempotent, ein Link auf eine andere
Community-Identity ein kontrollierter Konflikt. ResolveExternalIdentity,
GetExternalIdentityMapping, ListExternalIdentityMappings und UnlinkExternalIdentity
bilden die interne Application-Grenze. Unlink löscht nur die Integrations-owned
Mappingzeile; unbekannte externe IDs erzeugen keine Community-Identity.

Integrations:1:CreateExternalIdentityMappings besitzt ausschließlich die Tabelle
integration_external_identity_mappings. Ihr Primary Key auf provider_key plus
external_user_id schützt die Eindeutigkeit auch bei parallelen Link-Versuchen. Es gibt
keinen Foreign Key auf community_identities oder andere Modultabellen. Der API-Host
bindet AddIntegrationsModule() und die vier Routen unter
/api/admin/integrations/external-identities ein. Diese Management-Grenze ist in
Administration V1 über `Integrations.Read` beziehungsweise `Integrations.ManageMappings`,
Cookie-Authentication und Anti-Forgery geschützt; Integrations bleibt Owner des Mappings.
Twitch OAuth/EventSub, Streamer.bot, OBS, Razor/MVC, RBAC und Plugin-Infrastruktur sind
nicht Bestandteil dieses Slices. Details stehen in [integrations.md](integrations.md).

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

Economy besitzt den persistierten, community-bezogenen Saldo einer internen
`CommunityIdentityId`. `EconomyBalance` ist ein unveränderlicher, nicht-negativer
`long`-Wert; Credit schützt vor Overflow, Debit vor Überziehung. Der interne Store führt
beide Mutationen mit `SELECT FOR UPDATE` aus.

Neben den normalen atomaren Store-Pfaden existieren transaction-aware Overloads für Credit
und Debit ohne eigenen Commit. `Economy.Contracts` veröffentlicht ausschließlich
`IEconomyBalanceCredit` und `IEconomyBalanceDebit`, jeweils mit
`CommunityIdentityId`, Betrag, `DbConnection` und `DbTransaction`. Rewards verwendet
Credit; der Shop-Purchase verwendet Debit. Economy kennt keinen der Aufrufer und behält
Domain-, Lock- und Tabellenownership vollständig selbst.

`AddEconomyDebitCapability()` veröffentlicht für gemeinsame atomare Flows nur Store, Debit-
Capability und die bestehende Migration; Credit und die normalen Application-Use-Cases bleiben
dabei unregistriert. Der API-Host nutzt diese Capability ausschließlich innerhalb des Shop-
Purchases und bietet keinen Economy-Endpunkt. Eine konkrete Währungsbezeichnung, Multi-Currency,
Transfers, Ledger und Economy-eigenes Messaging gehören weiterhin nicht zum Slice. Details stehen
in [economy.md](economy.md).

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
XP bleiben Progression-owned. Die Inventory-Foundation existiert unabhängig von Rewards;
eine Inventory-Reward-Definition und Title-Rewards folgen erst in eigenen späteren Slices.
Es gibt noch keinen Runtime-Trigger und keine Worker-Anbindung; die API bindet Rewards nur
über die geschützte Administration-Managementgrenze ein.

## Aktueller Stand des Inventory-Moduls

Inventory besitzt den persistierten mengenbasierten Bestand mit
`CommunityIdentityId + ItemDefinitionId` als fachlicher Position. Der
`CommunityInventoryStore` verwendet PostgreSQL, Dapper und `SELECT FOR UPDATE`; Add legt
fehlende Positionen lazy an, Remove erzeugt keine fehlenden Positionen und löscht Zeilen bei
Bestand null. `Inventory:1:CreateCommunityInventoryEntries` bleibt unverändert und besitzt
keinen Cross-Module-Foreign-Key.

`Inventory.Contracts` enthält `ItemDefinitionId` und die caller-neutrale
`IInventoryQuantityGrant`-Capability. Sie nimmt zusätzlich
`CommunityIdentityId`, einen positiven Betrag sowie `DbConnection` und
`DbTransaction` entgegen und führt keinen eigenen Commit aus. Der Adapter delegiert an
denselben transaction-aware Add-Kern und bewahrt dadurch Domain-, Row-Lock- und
Sparse-Lifecycle. Shop ist der erste Aufrufer, Inventory kennt Shop jedoch nicht.

`AddInventoryGrantCapability()` registriert für gemeinsame atomare Flows nur Store, Grant-
Capability und die bestehende Migration; normale Add-/Remove-Use-Cases werden dabei nicht
aktiviert. Der API-Host nutzt diese Capability innerhalb des Shop-Purchases und bindet für die
Administration zusätzlich owner-owned Read-/Add-/Remove-Pfade ein. Item-Katalog, Messaging,
Rewards-Ausführung und Worker bleiben außerhalb des Inventory-Moduls. Details stehen in
[inventory.md](inventory.md).

## Shop-V1-Endzustand

Shop besitzt als zusammenhängendes Modul die Angebotsdomain, den persistierten und
betreibersteuerbaren Angebotskatalog, den atomaren Inventory-Kauf, die unveränderliche
Kaufhistorie, die direkte Storefront sowie die getrennte HTTP-Management-Grenze. Die
Archivierung ist terminal.
`Shop.Contracts` veröffentlicht `ShopOfferId`, `ShopPurchaseId`,
`ShopPurchaseRequestId` sowie `ShopPurchaseCompletedIntegrationEvent` mit
`shop.purchase-completed` v1.

Der bestehende Katalog bleibt unverändert in `shop_offers`; Mutationen verwenden
`SELECT FOR UPDATE`. `Shop:2:CreateShopPurchases` ergänzt
`shop_purchase_requests`, `shop_purchase_guards` und `shop_purchases`. Der einzige
Foreign Key ist Shop-intern von Purchase auf Offer. Identity, Economy und Inventory werden
nicht relational gekoppelt.

`PurchaseShopOffer` erzeugt die Purchase-ID serverseitig und delegiert an den
`PostgreSqlShopPurchaseExecutor`. Dieser besitzt eine gemeinsame
`PostgreSqlTransaction` und koordiniert Idempotenz-Reservation, Identity-Existenzprüfung,
stabilen Offer-Snapshot mit `FOR SHARE`, Kauflimit-Guard mit `FOR UPDATE`,
transaction-aware Economy-Debit, Inventory-Grant um exakt eins, Purchase-Write und Outbox.
Ein Fehler rollt sämtliche Effekte gemeinsam zurück; ein identischer Request erzeugt exakt
einen Kauf.

Shop referenziert dabei ausschließlich fremde Contracts, niemals fremde
Implementierungsassemblies oder Tabellen. `FlurNetz.Api` registriert `AddShopModule()` und
stellt Storefront-, History-, Purchase- und eine getrennte
`/api/admin/shop/offers`-Management-Grenze bereit. Diese ist in Administration V1 über
`Shop.Read`/`Shop.Manage`, Anti-Forgery, High-Risk-Reason/RequestId sowie Audit und
idempotente Operations geschützt. Die Management-Grenze verwendet die
vorhandenen Katalog-Use-Cases und eigene API-Verträge; sie sieht den vollständigen internen
Katalog, während die Storefront weiterhin nur `IsEnabled && !IsArchived && IsAvailableAt(now)`
erfüllt.
Der HTTP-Adapter führt dafür keine neue Migration oder fachliche Shop-Änderung ein; er führt
selbst keine Events oder Consumer aus. Die Shop-Administration bleibt ein hostseitiger
Management-Adapter; Fachzustand und Fachregeln bleiben im Shop-Owner.
Der separate Worker referenziert
für `shop.purchase-completed` v1 nur `Shop.Contracts`, kennt den Eventtyp explizit und
registriert den fachlichen Notifications-Consumer `notifications.shop-purchase`. Die Shop-
Implementierung und Shop-Migrationen werden dort nicht geladen. Warenkorb, variable Kaufmengen, Stock, Kategorien, zusätzliche Metadaten,
Discounts, Coupons, Refunds und Cancellation bleiben ebenfalls bewusst außerhalb des
Shop-V1-Scope. Details stehen in
[shop.md](shop.md).

## Notifications-V1-Endzustand

Notifications besitzt mit `CommunityNotification` eine persistente persönliche In-App-Inbox.
Die Domain speichert NotificationType, Title, Message und SourceReference als historischen
Snapshot und erzwingt kanonische Unicode- und UTC-/Mikrosekundenwerte. Die eigene Migration
`Notifications:1:CreateCommunityNotifications` erstellt `community_notifications` ohne
Cross-Module-Foreign-Keys. Application-Use-Cases decken Create, Get, identity-isolierte
Keyset-Liste, Unread Count, Mark Read, Mark Unread und Mark All Read ab.

Der Worker konsumiert `shop.purchase-completed` v1 ausschließlich aus `Shop.Contracts` und
schreibt die Notification über den transaction-aware Store-Insert gemeinsam mit dem
Messaging-Inbox-Eintrag. Der API-Host bindet die persönliche Inbox mit eigenen HTTP-DTOs ein,
führt aber keinen Consumer aus. `Notifications.Contracts` veröffentlicht ausschließlich die
caller-neutrale `ICommunityNotificationCreate`-Capability; externe Delivery-
Kanäle, Preferences, Templates, Delete/Archive/Retention und historischer Shop-Backfill sind
nicht Teil von V1. Details stehen in [notifications.md](notifications.md).

## Automation-V1-Endzustand

Automation besitzt eine eigene persistierte Rule Engine für die beiden vorhandenen Events
engagement.message-recorded und shop.purchase-completed. Das Aggregate AutomationRule erzwingt
Lifecycle, Textgrenzen, Trigger-/Condition-Kompatibilität, AND-Semantik, stabile Action-
Positionen und terminale Archivierung. Die vier Automation-eigenen Tabellen gehören der
Migration Automation:1:CreateAutomationRulesAndExecutions; Cross-Module-Foreign-Keys gibt es
nicht.

Die beiden expliziten Worker-Consumer automation.engagement-message-recorded und
automation.shop-purchase-completed laden stabile Rule-Snapshots mit FOR SHARE und reservieren
AutomationExecutions idempotent. Economy und Notifications werden ausschließlich über ihre
transaction-aware Contracts in der bestehenden Messaging-Transaktion geschrieben.
Die Management-API unter /api/admin/automation/rules ist API-owned, permission-geschützt und
anti-forgery-gesichert; der API-Host führt keine Consumer aus. Automation.Contracts bleibt
leer. Cron, Scheduler, eigene Queues, Replay,
Backfill, Delete, Run Now und Dry Run gehören nicht zum V1-Scope. Details stehen in
[automation.md](automation.md).

## Aktueller Stand des Titles-Moduls

Titles besitzt jetzt neben seiner Domain-Foundation einen persistierten Community-State und
einen separaten persistierten Definitionskatalog. `TitleDefinitionId` ist eine stabile,
nicht leere Guid-Fachkennung. `TitleDefinition` speichert im Katalog ausschließlich die ID,
einen kanonischen Anzeigenamen und eine optionale Beschreibung.

`CommunityTitles.Rehydrate` rekonstruiert gespeicherte Unlocks und die optionale aktuelle
Auswahl, ohne beschädigte Zustände zu reparieren. Die Community-Application-Schicht enthält
`UnlockCommunityTitle`, `LockCommunityTitle`, `SetCurrentCommunityTitle` und
`ClearCurrentCommunityTitle`; die Use Cases delegieren an den synchronen
`ICommunityTitlesStore`. `CommunityTitlesStore` persistiert über PostgreSQL und Dapper in
atomaren Read/Modify/Write-Transaktionen mit Root-Zeilensperre.

Der Katalog bietet die internen Use Cases Create, Get, List, Rename und
ChangeDescription. `TitleDefinitionStore` verwendet bei Mutationen `SELECT FOR UPDATE`
und persistiert nur tatsächliche Änderungen. Unbekannte Definitionen führen bei Mutationen
zu `TitleDefinitionNotFoundException`, während Get `null` liefert.

Titles besitzt die unveränderte Migration `Titles:1:CreateCommunityTitles` für
`community_titles`, `community_title_unlocks` und `community_title_selections` sowie
`Titles:2:CreateTitleDefinitions` für `title_definitions`. Die Community-Tabellen besitzen
interne Foreign Keys; `title_definitions` besitzt keine Foreign Keys. Es gibt ausdrücklich
keinen Unlock→Definition-Foreign-Key und keine Katalog-Existenzprüfung beim Unlock.
`community_identity_id` bleibt ein fachlicher Identifier ohne Cross-Module-Foreign-Key.
`TitlesModule` registriert beide Stores, alle internen Use Cases und beide Migrationen;
der API-Host bindet den Katalog sowie die communitybezogenen Read-/Unlock-/Lock-Management-
Routen über explizite Administration-Permissions ein. Der Owner bleibt für den Zustand
verantwortlich.

`FlurNetz.Modules.Titles.Contracts` bleibt leer. Messaging, Rewards-, Achievement- und
Shop-Anbindung sowie Worker und Overlay bleiben bewusst außerhalb dieses Slices; die
administrative API/UI ist eine hostseitige Composition über die bestehenden Owner-Use-Cases.
Echte PostgreSQL-Integrationstests prüfen Migration, Katalog-Constraints, Create/Get/List,
Rename, Description-Änderung, Rollback, Rehydration und Nebenläufigkeit. Details stehen in
[titles.md](titles.md).

## Aktueller Stand des Achievements-Moduls

Achievements besitzt den ersten persistierten Vertical Slice für zwei fachliche Bereiche:
einen implementation-eigenen Definitionskatalog und permanente Community-Achievements. Das
Modul kennt keine Runtime-Ereignisse und wird in diesem Schritt nicht durch API oder Worker
ausgelöst.

`AchievementDefinitionId` ist eine immutable, Guid-basierte Fachkennung. `AchievementDefinition`
enthält ausschließlich ID, kanonisch getrimmten `DisplayName` und optionale `Description`.
Anzeigenamen sind nicht leer und höchstens 100 `string.Length`-Zeichen lang; Beschreibungen
werden aus leerem oder Whitespace-Input als `null` kanonisiert und sind höchstens 500 Zeichen
lang. `Create` normalisiert Aufrufer-Input. `Rehydrate` akzeptiert ausschließlich bereits
kanonische Persistenzwerte, damit beschädigte Daten nicht still repariert werden. `Rename` und
`ChangeDescription` liefern bei einem kanonischen No-op `false`.

`CommunityAchievement` enthält ausschließlich `CommunityIdentityId`,
`AchievementDefinitionId` und den unveränderlichen `UnlockedAtUtc`. Beide IDs müssen
strukturell gültig sein; der Zeitpunkt muss als UTC mit `Offset == TimeSpan.Zero` vorliegen.
Ob eine Community-Identität in Identity persistiert ist, prüft Achievements nicht.

Die internen Application-Use-Cases sind `CreateAchievementDefinition`,
`GetAchievementDefinition`, `ListAchievementDefinitions`, `RenameAchievementDefinition`,
`ChangeAchievementDescription`, `UnlockCommunityAchievement`, `GetCommunityAchievement` und
`ListCommunityAchievements`. Der Unlock prüft die Definition im eigenen Katalog, bezieht den
Zeitpunkt aus `IClock` und übergibt ein gültiges Domainobjekt an den Community-Store. Der erste
erfolgreiche Write gewinnt; ein Duplicate ist ein normaler idempotenter No-op und überschreibt
den ursprünglichen Unlock-Zeitpunkt nicht.

`AchievementDefinitionStore` verwendet für Mutationen eine Transaktion mit
`SELECT ... FOR UPDATE` und einem synchronen Domain-Callback. No-ops führen zu keinem Update.
`CommunityAchievementStore` verwendet einen einzelnen PostgreSQL-Insert mit
`ON CONFLICT (community_identity_id, achievement_definition_id) DO NOTHING`. Es gibt keine
vorgelagerte Existenzabfrage, keine künstliche Root-Zeile und keine globale Community-Sperre.

Die Migration `Achievements:1:CreateAchievementDefinitionsAndCommunityAchievements` legt im
`public`-Schema zuerst `achievement_definitions` und danach `community_achievements` an. Der
Composite Primary Key der Community-Tabelle deckt den aktuellen Lookup-Bedarf ab. Es existiert
genau ein interner Foreign Key von `achievement_definition_id` auf `achievement_definitions`;
es gibt ausdrücklich keinen Foreign Key auf `community_identities` oder ein anderes Modul.

Das Implementierungsprojekt referenziert neben dem leeren eigenen Contract ausschließlich
`FlurNetz.Modules.Identity.Contracts`, `FlurNetz.BuildingBlocks` und `FlurNetz.Persistence`.
`FlurNetz.Modules.Achievements.Contracts` bleibt vollständig leer. Unit-Tests prüfen Domain-
und Application-Semantik; echte PostgreSQL-Integrationstests prüfen Migration, Constraints,
Rehydration, Rollback, Idempotenz und parallele Unlocks beziehungsweise Katalogmutationen.

Ausgeschlossen bleiben Progress, Counter, TargetValue, Regeln, Conditions, Evaluator,
Trigger-Konfiguration, Domain- und Integration-Events, Messaging, Inbox, Outbox, Worker, API,
Rewards-, Economy-, Inventory-, Titles-, Shop-, Notifications- und Overlay-
Anbindung, Seed-Daten, Standard-Achievements, Delete, Revoke, Reset, Archive, Enable/Disable,
Hidden/Secret, Localization, Icon, Farbe, Rarity, Category, Points, SortOrder, Slug,
TechnicalName und RewardPackageId.

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
ausführbaren Rewards-Slice; sein eigenes Contracts-Projekt bleibt leer. Inventory besitzt Domain,
interne Use Cases, atomaren Store, Migration und Registrierung; sein Contracts-Projekt enthält
`ItemDefinitionId`. Titles besitzt Domain, Rehydration, `TitleDefinition`, interne
Application-Use-Cases, getrennte Community- und Katalog-Stores, zwei Migrationen, Modulregistrierung
und echte Integrationstests; Achievements besitzt Domain, Application, getrennte Katalog- und
Community-Stores, eine Migration, Modulregistrierung und echte Integrationstests; Shop besitzt den
persistierten `Shop-Katalog` mit minimalem `ShopOfferId`-Contract, den vier fachlichen
Migrationen `Shop:1:CreateShopOffers` bis `Shop:4:AddShopOfferArchiveState`, internen Use Cases,
PostgreSQL-/Dapper-Store, Row-Lock-Mutationen und terminaler Angebotsarchivierung sowie gezielte
read-only Storefront- und Purchase-History-Queries. Shop bewertet Textgrenzen
nach Unicode-Skalarwerten passend zu PostgreSQL, verwirft U+0000 und nicht wohlgeformtes UTF-16
und modelliert Availability-Grenzen als UTC-Instants mit mikrosekundengenauer Präzision. Die übrigen Contracts-Projekte
bleiben leer.

Die Implementierungs-Assembly ist der Ort für Domain, Application, interne
Persistence-Adapter, interne Event Handler und die Modulregistrierung. Identity nutzt davon
aktuell nur Domain, Application, den Persistenzadapter, die fachliche Migration und die
Registrierung der tatsächlich vorhandenen Komponenten. Engagement nutzt dieselben Schichten
für seinen Message-Recording-Slice und registriert Use Case, Repository, Migration und Uhr.
Progression nutzt Domain, Application, einen atomaren Store, Migration, Consumer und Registrierung;
der unabhängige Worker-Host verdrahtet diesen Slice für die Runtime. Economy nutzt Domain,
Application, einen atomaren Store, Migration und Registrierung; der API-Host verdrahtet für den
Shop-Purchase ausschließlich die schmale Debit-Capability, aber keine Economy-HTTP-Endpunkte.
Rewards nutzt Domain, Application, gezielte Katalog- und Grant-Persistence, Migration und
Registrierung; der API-Host verdrahtet die explizite read-/create-/grant-Management-Grenze,
aber keine Runtime-Trigger. Inventory nutzt Domain, Application, einen atomaren PostgreSQL-
Store, Migration und Registrierung; der API-Host verdrahtet zusätzlich die schmalen Read-/Add-
/Remove-Managementpfade für Administration und weiterhin die Grant-Capability im Shop-
Purchase. Shop nutzt Domain, Application, den gezielten `ShopOfferStore`,
die Purchase-History-Stores, vier fachliche Migrationen sowie `ShopModule` mit Read-Basis;
der Mutation-Callback ist als `Func<ShopOffer, bool>` synchron begrenzt. Der API-Host verdrahtet
`AddShopModule()`, mappt Storefront-, History-, Purchase- und die getrennte Management-
Endpoint-Gruppe auf die vorhandenen Use-Cases. Der Worker kennt `shop.purchase-completed` v1 über
`Shop.Contracts`, registriert den Notifications-Consumer und lädt weder die Shop-
Implementierung noch ihre Migrationen. Notifications nutzt Domain, Application, einen gezielten
PostgreSQL-Store, eine Migration und Registrierung; die API mappt die persönliche Inbox, der
Worker registriert zusätzlich die Consumer-Policy. Titles nutzt Domain, Rehydration, Application, getrennte
Community- und Katalog-PostgreSQL-Stores, zwei Migrationen und Registrierung; Achievements nutzt
Domain, Application, getrennte Katalog- und Community-PostgreSQL-Stores, eine Migration und
Registrierung; API bindet die expliziten Administration-Reads und Mutationen ein, Worker nicht.
Die übrigen
Implementierungs-Assemblies bleiben fachlich leer.

Eine Implementierung darf keine andere Modulimplementierung direkt referenzieren. Engagement
darf den eigenen Contract, `Identity.Contracts` sowie die ausdrücklich erlaubten technischen
BuildingBlocks-, Persistence- und Messaging-Projekte verwenden. Progression darf zusätzlich
ausschließlich `Engagement.Contracts` und Messaging verwenden; die Engagement-Implementierung
bleibt verboten. Economy darf `Identity.Contracts` und seinen eigenen öffentlichen
Capability-Contract verwenden; Rewards darf zusätzlich `Identity.Contracts` und
`Economy.Contracts` verwenden und referenziert keine Economy-Implementierung. Inventory darf
zusätzlich `Identity.Contracts` und die technische Persistence-Assembly verwenden; Rewards und
Messaging bleiben verboten. Shop verwendet `Shop.Contracts`, `Identity.Contracts`,
`Economy.Contracts`, `Inventory.Contracts`, Messaging und die technische
`FlurNetz.Persistence`-Assembly, aber keine fremde Modulimplementierung. Notifications verwendet
zusätzlich `Identity.Contracts`, `Shop.Contracts`, Messaging und `FlurNetz.Persistence`; die
Notifications-Implementierung bleibt ohne fremde Modulimplementierung und
`Notifications.Contracts` veröffentlicht ausschließlich die caller-neutrale
`ICommunityNotificationCreate`-Capability. Titles und Achievements dürfen zusätzlich `Identity.Contracts`
und die technische Persistence-Assembly verwenden; Achievements verwendet außerdem
`FlurNetz.BuildingBlocks` für `IClock`. Messaging und alle fachlichen Modulimplementierungen bleiben
verboten.
Cross-Module-Kommunikation erfolgt über freigegebene öffentliche Contracts
und Integration Events. Es gibt keine gemeinsamen fachlichen Domain-Modelle und keine
vorsorglichen Shared-Entities.

Die Administration besitzt zusätzlich eigene Unit-, PostgreSQL-Integration-, API- und
Architekturtests für Credentials, Bootstrap, Recovery, Audit, Operations, Policies, CSRF,
Atomizität, Parallelität und Secret-Redaction. Die modulbezogenen Testprojekte bleiben für die übrigen Module technisch minimal. Die Identity-
und Engagement-Unit- sowie PostgreSQL-Integrationstests prüfen jeweils die vorhandenen Domain-
und Use-Case-Flows, Migration, Commit/Rollback, Primärschlüssel und Laden. Progression wird
zusätzlich mit Domain-, Use-Case-, Migration-, Rollback-, Load- und echten PostgreSQL-
Concurrency-Tests abgesichert. Economy besitzt eigene Domain-, Use-Case-, Migration-, Lifecycle-,
Rollback-, Load- und echte PostgreSQL-Concurrency-Tests. Rewards besitzt Domain- und
Application-Unit-Tests, Architekturtests sowie ein eigenes echtes PostgreSQL-
Integrationstestprojekt für Migration, Katalog, Atomicity, Idempotenz und Nebenläufigkeit.
Inventory besitzt Domain- und Application-Unit-Tests, eigene Architekturgrenzen sowie echte PostgreSQL-Integrationstests für Migration, Sparse-Lifecycle, Rollback und Nebenläufigkeit. Titles besitzt Domain- und Application-Unit-Tests, eigene Architekturtests sowie echte PostgreSQL-Integrationstests für Migration, Constraints, Rollback, Rehydration und Nebenläufigkeit. Achievements besitzt Domain- und Application-Unit-Tests, eigene Architekturtests sowie echte PostgreSQL-Integrationstests für Migration, Katalog-Constraints, Rehydration, Rollback, idempotente Unlocks und Nebenläufigkeit.
Das separate `FlurNetz.Workflows.IntegrationTests`-Projekt prüft
den vollständigen Outbox-/Inbox-Weg sowie Producer- und Consumer-Atomicity gegen PostgreSQL.
Die Architecture Tests prüfen zusätzlich Event Ownership, Contract-Minimalität, erlaubte
Messaging-Kanten, die Rewards-, Inventory- und Notifications-Abhängigkeitsgrenzen sowie die
Consumer-Grenzen automatisiert. Notifications besitzt eigene Unit-, PostgreSQL-, Workflow-,
API- und Architekturtests für Snapshot, Pagination, Identity-Isolation, Read-/Unread-Lifecycle,
Atomicity und Inbox-Deduplizierung.

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
13. Integrations (umgesetzt: External-Identity-Mapping V1)
14. Administration (umgesetzt: Administration V1)

Diese Reihenfolge dokumentiert die Umsetzung. Administration ist der hostseitige
Security-/Operations-Slice für die vorhandenen Owner-Module; weitere fachliche
Identity-Funktionalität folgt erst mit konkretem Bedarf.

## Cross-Cutting-Fähigkeiten

Audit und Analytics werden in diesem Schritt nicht als eigene Assemblies angelegt. Sie werden erst eingeführt, wenn reale fachliche Aktionen und Events einen konkreten Bedarf dafür erzeugen.
