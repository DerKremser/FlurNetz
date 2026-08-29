# Änderungsprotokoll

## [Unveröffentlicht]

### Hinzugefügt

- Initiales Repository- und Solution-Grundgerüst für FlurNetz.
- Technische PostgreSQL-Persistenzgrundlage mit Npgsql und Dapper.
- SQL-first Migration Runner mit Migration Ownership, Migration-History und unveränderlichen SQL-Checksums.
- Unit-, Architektur- und echte PostgreSQL-Integrationstests für Verbindungen, Transaktionen und Migrationen.
- Domain-neutrale BuildingBlocks-Grundlage mit Result-/Error-Primitives, Guards und Clock-Abstraktion.
- Erste automatisierte Architekturtests zur Absicherung zentraler Projektgrenzen.
- Messaging Foundation mit getrennten Domain- und Integration-Event-Verträgen sowie deterministischem In-Process-Dispatcher.
- PostgreSQL-Outbox und Inbox für atomare, zuverlässige und deduplizierte Integration Events.
- Explizite Message-Type-Registry, versionierte System.Text.Json-Serialisierung, Claiming sowie Retry-/Failed-Fehlerbehandlung.
- Unit-, Architecture- und echte PostgreSQL-Integrationstests für atomare Verarbeitung, transactional Inbox, Duplicate Redelivery, paralleles Claiming und Poison Messages.
