# Änderungsprotokoll

## [Unveröffentlicht]

### Hinzugefügt

- Erster Engagement-Vertical-Slice zum Aufzeichnen normalisierter Message-Aktivitäten.
- Engagement-eigene PostgreSQL-Migration und Persistenz für Community-Aktivitäten.
- Fachliche Engagement-Grundlage für normalisierte Community-Aktivitäten.
- Engagement-Aktivitäten verwenden die interne `CommunityIdentityId` statt externer Plattformidentitäten.
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
- Physische Contracts- und Implementierungsprojekte für alle vorgesehenen Fachmodule.
- Modulbezogene xUnit-v3-Testprojekte und Architekturtests zur Absicherung der Modul- und Assembly-Grenzen.
- Erste fachliche Identity-Grundlage mit stabiler interner Community-Identity-ID.
- Minimales Domain-Modell für die interne Community-Identität.
- Erster Identity-Vertical-Slice zum Erzeugen, Persistieren und Laden einer internen Community-Identität.
- Identity-eigene PostgreSQL-Migration für die minimale `community_identities`-Tabelle sowie echte PostgreSQL-Integrationstests.
- Erster ausführbarer ASP.NET-Core-API-Host als Composition Root.
- HTTP-Endpunkt `POST /api/identities` zur Erzeugung interner Community-Identitäten.
- Echte API-Integrationstests vom HTTP-Request bis zur PostgreSQL-Persistierung.
