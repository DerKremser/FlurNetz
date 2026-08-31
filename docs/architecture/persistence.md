# Persistence Foundation

`FlurNetz.Persistence` ist die technische PostgreSQL-Infrastruktur von FlurNetz. Sie enthält keine Fachmodule, fachlichen Tabellen, fachlichen Repositories oder fachlichen Services.

## Datenzugriff

PostgreSQL ist die primäre relationale Datenbank. Npgsql stellt über eine gemeinsam verwaltete `NpgsqlDataSource` die asynchrone Connection-Erzeugung bereit. `PostgreSqlConnectionFactory` öffnet konfigurierte Connections; Connection Strings werden nicht im Repository hinterlegt.

Dapper ist die schlanke SQL-Ausführungsbasis. Es gibt bewusst keinen ORM und kein Generic Repository. Fachliche Persistence-Adapter schreiben ihre gezielten SQL-Queries später selbst.

## Transaktionen

`PostgreSqlTransaction` besitzt genau eine geöffnete Connection und deren PostgreSQL-Transaction. `BeginAsync`, `CommitAsync`, `RollbackAsync` und `DisposeAsync` arbeiten asynchron und unterstützen `CancellationToken`. Wird eine aktive Transaction disposed, wird sie zurückgerollt. Dadurch können spätere technische oder fachliche SQL-Operationen dieselbe Connection und Transaction verwenden.

Diese technische Grenze ermöglicht bewusst konkrete atomare Kompositionen zwischen
fachlichen Modulen. Der Rewards-Executor führt seine Grant-Records und die Economy-
Gutschrift über den öffentlichen `IEconomyBalanceCredit`-Contract mit derselben Connection
und Transaction aus.

Der Shop-Purchase-Executor ist die zweite konkrete Komposition. Er besitzt eine
`PostgreSqlTransaction` und koordiniert darin seine Request-/Guard-/Purchase-Writes,
`ICommunityIdentityExistence`, `IEconomyBalanceDebit`,
`IInventoryQuantityGrant` und den vorhandenen `IIntegrationEventPublisher`. Die fremden
Module führen innerhalb der bereitgestellten `DbConnection`/`DbTransaction` keinen eigenen
Commit aus. Dadurch werden Shop-Kauf, Economy-Debit, Inventory-Grant und Outbox gemeinsam
bestätigt oder zurückgerollt.

Das ist weiterhin kein globales Unit-of-Work-Framework und kein generischer
Cross-Module-Repository-Vertrag; die jeweilige fachliche Transaktionsgrenze bleibt beim
aufrufenden Slice.

## SQL-first Migrationen

Migrationen sind explizite SQL-Texte und werden über `IMigrationSource` bereitgestellt. Die neutrale `MigrationSource` kann Migrationen verschiedener Besitzer aufnehmen; es gibt keine Reflection- oder Plugin-Infrastruktur.

Jede Migration besitzt eine eindeutige Identität aus:

- `Owner`: zuständiges Modul oder technische Infrastruktur
- `Version`: positive, innerhalb des Owners eindeutige Versionsnummer
- `Name`: lesbarer stabiler Name

Der `MigrationRunner` sortiert Migrationen deterministisch nach Owner, Version und Name. Doppelte Kombinationen aus Owner und Version werden vor jeder Datenbankänderung abgelehnt.

Rewards besitzt die Migration `Rewards:1:CreateRewardConfigurationAndGrants` selbst. Sie
legt die Rewards-eigenen Tabellen und ausschließlich deren interne Foreign Keys an. Die
fachliche `community_identity_id` sowie die Zusammenarbeit mit `community_economies` bleiben
Cross-Module-Beziehungen ohne Datenbank-Foreign-Key. Inventory besitzt
`Inventory:1:CreateCommunityInventoryEntries` mit Composite Primary Key aus
`community_identity_id + item_definition_id` und einem Nichtnegativ-Check für `quantity`.
Die Tabelle enthält ebenfalls keine Cross-Module-Foreign-Keys.
Titles besitzt die unveränderte Migration `Titles:1:CreateCommunityTitles` für die drei
Community-State-Tabellen `community_titles`, `community_title_unlocks` und
`community_title_selections`. Die neue Migration `Titles:2:CreateTitleDefinitions` legt
zusätzlich ausschließlich `title_definitions` an. Die drei Community-Tabellen verwenden
interne Foreign Keys; insbesondere existiert kein Foreign Key auf `community_identities`.
`title_definitions` besitzt keine Foreign Keys und es gibt keinen Unlock→Definition-FK.

## Migration-History

Der Runner legt bei Bedarf die technische Tabelle `flurnetz_persistence.migration_history` an. Sie speichert Owner, Version, Name, Anwendungszeitpunkt und die SHA-256-Checksum des SQL-Inhalts. Die History ist technische Metadaten und keine fachliche Tabelle.

Bereits angewendete Migrationen werden übersprungen, wenn Identität und Checksum unverändert sind. Wird derselbe Owner/Version mit anderem Namen oder verändertem SQL erneut bereitgestellt, schlägt der Lauf klar fehl; angewendete Migrationen werden nicht stillschweigend überschrieben. Jede Migration und ihr History-Eintrag werden in derselben PostgreSQL-Transaction ausgeführt. Ein SQL-Fehler rollt daher auch die Migration und ihre Registrierung zurück.

Der ausführbare API-Host stellt die Connection-Konfiguration als Composition Root bereit und
ruft den bestehenden Runner vor dem Listener-Start auf. Ein Fehler wird geloggt und beendet den
Startup, damit kein nicht initialisierter Host als betriebsbereit erscheint. Der erste fachliche
Besitzer einer Migration ist Identity: `Identity:1:CreateCommunityIdentities` legt die Tabelle
`community_identities` mit ausschließlich `id uuid primary key` an. Engagement besitzt nun als
weiteres Modul die Migration `Engagement:1:CreateEngagementActivities` für seine Tabelle
`engagement_activities`. Beide SQL-Quellen liegen in ihren Modulen; `FlurNetz.Persistence`
bleibt frei von fachlichen Tabellen und Migrationen. Die fachliche `community_identity_id` in
Engagement ist ein Cross-Module-Identifier und erzeugt bewusst keinen Foreign Key auf die
Identity-Tabelle. Progression besitzt zusätzlich die Migration
`Progression:1:CreateCommunityProgressions` für `community_progressions`. Die Tabelle enthält
nur `community_identity_id uuid primary key` und `experience_points bigint not null` mit einem
Nichtnegativ-Check. Die fachliche ID bleibt ebenfalls ein Cross-Module-Identifier ohne Foreign
Key. Die atomare Progression-Mutation initialisiert eine fehlende Zeile gezielt, sperrt sie mit
`SELECT FOR UPDATE`, führt die Domain-Mutation aus und aktualisiert sie innerhalb derselben
`PostgreSqlTransaction`; dadurch werden Lost Updates bei parallelen Writes verhindert. Economy
besitzt zusätzlich die Migration `Economy:1:CreateCommunityEconomies` für
`community_economies`. Die Tabelle enthält ausschließlich
`community_identity_id uuid primary key` und `balance bigint not null` mit einem
Nichtnegativ-Check; auch hier gibt es keinen Foreign Key auf Identity. Credits und Debits führen
ihre Read/Modify/Write-Sequenz in einer eigenen `PostgreSqlTransaction` mit
`SELECT FOR UPDATE` aus. Ein Credit legt die Zeile erst bei einer erfolgreichen fachlichen
Gutschrift lazy an; ein fehlgeschlagener Debit auf eine fehlende Zeile erzeugt keine Zeile.
Rewards besitzt zusätzlich eigene Tabellen für Definitionen, Packages, Package-Memberships
und eindeutige Grant-Records; diese Tabellen werden zusammen mit Economy nur über eine
gemeinsame PostgreSQL-Transaktion koordiniert. Inventory besitzt zusätzlich
`community_inventory_entries`. Der Store initialisiert eine fehlende Position nur im Add-Pfad,
sperrt die Composite-Key-Zeile mit `SELECT FOR UPDATE` und löscht sie wieder, sobald Remove den
Bestand exakt auf null reduziert. Ein fehlender Remove erzeugt keine Zeile. Der Titles-Store
legt eine fehlende Root-Zeile lazy als Lock-Anker an, sperrt sie mit `SELECT FOR UPDATE`,
rehydriert Unlocks und Current und persistiert den Zustands-Diff in derselben atomaren
Read/Modify/Write-Transaktion. Neue Unlocks werden vor der Selection geschrieben, entfernte
Unlocks erst danach; die interne Selection→Unlock-Fremdschlüsselbeziehung bleibt dadurch
auch während des Writes gültig. Der Root-Lock serialisiert nur Operationen derselben
`CommunityIdentityId`. Der `TitleDefinitionStore` führt Create in einer eigenen Transaktion
aus und verwendet bei Rename sowie Description-Änderung einen Row-Lock mit
`SELECT FOR UPDATE`; ein UPDATE erfolgt nur bei tatsächlicher Domain-Änderung.

Shop besitzt zusätzlich die unveränderte Migration `Shop:1:CreateShopOffers` und
`Shop:2:CreateShopPurchases`. V2 legt `shop_purchase_requests`,
`shop_purchase_guards` und `shop_purchases` an. Der einzige Foreign Key dieser
Purchase-Migration ist Shop-intern von `shop_purchases.shop_offer_id` auf
`shop_offers.id`; Identity-, Economy- und Inventory-Beziehungen bleiben bewusst ohne
Cross-Module-Foreign-Key. Der Purchase-Executor verwendet einen `FOR SHARE`-Lock auf dem
Angebot und einen `FOR UPDATE`-Guard pro Offer/Identity, bevor er die transaction-aware
Capabilities der fremden Module aufruft.

## Tests

`FlurNetz.Persistence.IntegrationTests` prüft die Foundation gegen echtes PostgreSQL: Connection und `SELECT 1`, Commit, Rollback, leere Datenbank, History-Erzeugung, Migrationen, Idempotenz, deterministische Reihenfolge, Fehler-Rollback und Checksum-Änderungen. Der Engagement-Slice besitzt dafür ein eigenes Integration-Testprojekt mit Migration, Idempotenz, Message-Recording, Laden, Not-Found, Duplicate-PK, Rollback und unbekanntem Activity-Type. Der Progression-Slice besitzt eigene PostgreSQL-Tests für Migration, lazy Initialisierung, Domain-Rehydration, Rollback, Not-Found und parallele Grants gegen echte Zeilensperren. Der Economy-Slice prüft Migration, Lazy-Lifecycle, Laden, Debit-Fehler, Overflow-Rollback, Datenbank-Check und konkurrierende Credits sowie Debits gegen echte Zeilensperren. Der Rewards-Slice prüft in einem eigenen Testcontainers-Projekt Migration und Idempotenz, Katalogpersistenz, Package-Atomicity, Overflow-Rollback, Partial-State, parallele Duplicate-Grants und die gemeinsame Economy-Transaktion. Der Inventory-Slice besitzt eigene echte PostgreSQL-Tests für Composite Key, Sparse-Lifecycle, Rollback, Isolation mehrerer Bestandspositionen und konkurrierende Adds sowie Removes. Standardmäßig wird dafür eine isolierte PostgreSQL-Testinstanz über Testcontainers (`postgres:15.1`) verwendet. Docker muss für diese Testvariante verfügbar sein; alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` gesetzt werden.
`FlurNetz.Modules.Titles.IntegrationTests` prüft Titles V1 und V2, ihre Idempotenz, die
drei Community-State-Tabellen, `title_definitions`, interne Foreign Keys, Text-Checks, alle
vier atomaren Community-Operationen, Katalog-Create/Get/List/Rename/Description,
Rehydration, Rollback und echte Katalog-Concurrency gegen PostgreSQL. Standardmäßig wird
dafür eine isolierte PostgreSQL-Testinstanz über Testcontainers (`postgres:15.1`) verwendet.
Docker muss für diese Testvariante verfügbar sein; alternativ kann
`FLURNETZ_TEST_CONNECTION_STRING` gesetzt werden.
`FlurNetz.Modules.Shop.IntegrationTests` prüft zusätzlich den atomaren Purchase über die
realen Identity-, Economy-, Inventory- und Messaging-Adapter: erfolgreicher gemeinsamer
Commit, Duplicate-Request-Idempotenz, Idempotency-Conflict, konkurrierendes Kauflimit und
vollständiger Rollback bei unzureichendem Saldo.
