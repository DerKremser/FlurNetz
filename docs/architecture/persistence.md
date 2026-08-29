# Persistence Foundation

`FlurNetz.Persistence` ist die technische PostgreSQL-Infrastruktur von FlurNetz. Sie enthält keine Fachmodule, fachlichen Tabellen, fachlichen Repositories oder fachlichen Services.

## Datenzugriff

PostgreSQL ist die primäre relationale Datenbank. Npgsql stellt über eine gemeinsam verwaltete `NpgsqlDataSource` die asynchrone Connection-Erzeugung bereit. `PostgreSqlConnectionFactory` öffnet konfigurierte Connections; Connection Strings werden nicht im Repository hinterlegt.

Dapper ist die schlanke SQL-Ausführungsbasis. Es gibt bewusst keinen ORM und kein Generic Repository. Fachliche Persistence-Adapter schreiben ihre gezielten SQL-Queries später selbst.

## Transaktionen

`PostgreSqlTransaction` besitzt genau eine geöffnete Connection und deren PostgreSQL-Transaction. `BeginAsync`, `CommitAsync`, `RollbackAsync` und `DisposeAsync` arbeiten asynchron und unterstützen `CancellationToken`. Wird eine aktive Transaction disposed, wird sie zurückgerollt. Dadurch können spätere technische oder fachliche SQL-Operationen dieselbe Connection und Transaction verwenden.

## SQL-first Migrationen

Migrationen sind explizite SQL-Texte und werden über `IMigrationSource` bereitgestellt. Die neutrale `MigrationSource` kann Migrationen verschiedener Besitzer aufnehmen; es gibt keine Reflection- oder Plugin-Infrastruktur.

Jede Migration besitzt eine eindeutige Identität aus:

- `Owner`: zuständiges Modul oder technische Infrastruktur
- `Version`: positive, innerhalb des Owners eindeutige Versionsnummer
- `Name`: lesbarer stabiler Name

Der `MigrationRunner` sortiert Migrationen deterministisch nach Owner, Version und Name. Doppelte Kombinationen aus Owner und Version werden vor jeder Datenbankänderung abgelehnt.

## Migration-History

Der Runner legt bei Bedarf die technische Tabelle `flurnetz_persistence.migration_history` an. Sie speichert Owner, Version, Name, Anwendungszeitpunkt und die SHA-256-Checksum des SQL-Inhalts. Die History ist technische Metadaten und keine fachliche Tabelle.

Bereits angewendete Migrationen werden übersprungen, wenn Identität und Checksum unverändert sind. Wird derselbe Owner/Version mit anderem Namen oder verändertem SQL erneut bereitgestellt, schlägt der Lauf klar fehl; angewendete Migrationen werden nicht stillschweigend überschrieben. Jede Migration und ihr History-Eintrag werden in derselben PostgreSQL-Transaction ausgeführt. Ein SQL-Fehler rollt daher auch die Migration und ihre Registrierung zurück.

Der Host entscheidet später ausdrücklich, wann der Runner aufgerufen wird. Es gibt keine automatische Ausführung beim Prozessstart und keine fachliche Migration in dieser Foundation.

## Tests

`FlurNetz.Persistence.IntegrationTests` prüft die Foundation gegen echtes PostgreSQL: Connection und `SELECT 1`, Commit, Rollback, leere Datenbank, History-Erzeugung, Migrationen, Idempotenz, deterministische Reihenfolge, Fehler-Rollback und Checksum-Änderungen. Standardmäßig wird dafür eine isolierte PostgreSQL-Testinstanz über Testcontainers (`postgres:15.1`) verwendet. Docker muss für diese Testvariante verfügbar sein; alternativ kann `FLURNETZ_TEST_CONNECTION_STRING` gesetzt werden.
