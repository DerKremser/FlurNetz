# Persistence Foundation

`FlurNetz.Persistence` ist die technische PostgreSQL-Infrastruktur von FlurNetz. Sie enthält keine Fachmodule, fachlichen Tabellen, fachlichen Repositories oder fachlichen Services.

## Datenzugriff

PostgreSQL ist die primäre relationale Datenbank. Npgsql stellt über eine gemeinsam verwaltete `NpgsqlDataSource` die asynchrone Connection-Erzeugung bereit. `PostgreSqlConnectionFactory` öffnet konfigurierte Connections; Connection Strings werden nicht im Repository hinterlegt.

Dapper ist die schlanke SQL-Ausführungsbasis. Es gibt bewusst keinen ORM und kein Generic Repository. Fachliche Persistence-Adapter schreiben ihre gezielten SQL-Queries später selbst.

## Transaktionen

`PostgreSqlTransaction` besitzt genau eine geöffnete Connection und deren PostgreSQL-Transaction. `BeginAsync`, `CommitAsync`, `RollbackAsync` und `DisposeAsync` arbeiten asynchron und unterstützen `CancellationToken`. Wird eine aktive Transaction disposed, wird sie zurückgerollt. Dadurch können spätere technische oder fachliche SQL-Operationen dieselbe Connection und Transaction verwenden.

Diese technische Grenze ermöglicht auch eine bewusst konkrete atomare Komposition zwischen
fachlichen Modulen. Der Rewards-Executor führt seine Grant-Records und die Economy-
Gutschrift über den öffentlichen `IEconomyBalanceCredit`-Contract mit derselben Connection
und Transaction aus. Das ist kein globales Unit-of-Work-Framework und kein generischer
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
Titles besitzt die Migration `Titles:1:CreateCommunityTitles`. Sie legt in der bestehenden
`public`-Datenbank genau `community_titles`, `community_title_unlocks` und
`community_title_selections` an. Die drei Titles-Tabellen verwenden ausschließlich interne
Foreign Keys; insbesondere existiert kein Foreign Key auf `community_identities`.

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
`CommunityIdentityId`.

## Tests

`FlurNetz.Persistence.IntegrationTests` prüft die Foundation gegen echtes PostgreSQL: Connection und `SELECT 1`, Commit, Rollback, leere Datenbank, History-Erzeugung, Migrationen, Idempotenz, deterministische Reihenfolge, Fehler-Rollback und Checksum-Änderungen. Der Engagement-Slice besitzt dafür ein eigenes Integration-Testprojekt mit Migration, Idempotenz, Message-Recording, Laden, Not-Found, Duplicate-PK, Rollback und unbekanntem Activity-Type. Der Progression-Slice besitzt eigene PostgreSQL-Tests für Migration, lazy Initialisierung, Domain-Rehydration, Rollback, Not-Found und parallele Grants gegen echte Zeilensperren. Der Economy-Slice prüft Migration, Lazy-Lifecycle, Laden, Debit-Fehler, Overflow-Rollback, Datenbank-Check und konkurrierende Credits sowie Debits gegen echte Zeilensperren. Der Rewards-Slice prüft in einem eigenen Testcontainers-Projekt Migration und Idempotenz, Katalogpersistenz, Package-Atomicity, Overflow-Rollback, Partial-State, parallele Duplicate-Grants und die gemeinsame Economy-Transaktion. Der Inventory-Slice besitzt eigene echte PostgreSQL-Tests für Composite Key, Sparse-Lifecycle, Rollback, Isolation mehrerer Bestandspositionen und konkurrierende Adds sowie Removes. Standardmäßig wird dafür eine isolierte PostgreSQL-Testinstanz über Testcontainers (`postgres:15.1`) verwendet. Docker muss für diese Testvariante verfügbar sein; alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` gesetzt werden.
`FlurNetz.Modules.Titles.IntegrationTests` prüft die Titles-Migration und ihre Idempotenz,
die drei Tabellen, internen Foreign Keys, alle vier atomaren Operationen, Rehydration,
Rollback und konkurrierende Änderungen gegen echtes PostgreSQL. Standardmäßig wird dafür
eine isolierte PostgreSQL-Testinstanz über Testcontainers (`postgres:15.1`) verwendet.
Docker muss für diese Testvariante verfügbar sein; alternativ kann
`FLURNETZ_TEST_CONNECTION_STRING` gesetzt werden.
