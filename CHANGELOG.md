# Änderungsprotokoll

## [Unveröffentlicht]

### Hinzugefügt

- Minimale Rewards-Domain mit Reward Definitions, Packages, Sources und Grant-Records.
- Erste konkrete Reward Definition für eine spätere Economy-Balance-Gutschrift.
- Minimale Economy-Domain für nicht-negative Community-Salden.
- Fachliche Gutschriften und Abbuchungen mit Schutz vor Überziehung und Overflow.
- Erster persistierter Economy-Vertical-Slice für atomare Community-Salden.
- Nebenläufigkeitssichere Gutschriften und Abbuchungen mit PostgreSQL-Row-Locking.
- Economy-eigene Migration mit Datenbankinvariante für nicht-negative Salden.
- Erster dauerhaft laufender FlurNetz-Worker zur kontinuierlichen Verarbeitung der PostgreSQL-Outbox.
- Explizite Runtime-Komposition des Engagement-Message-Events mit dem Progression-Consumer.
- Erster zuverlässiger Cross-Module-Workflow von Engagement zu Progression über Outbox und Inbox.
- Normalisierte Message-Aktivitäten können über den Progression-Consumer einmalig 1 XP vergeben.
- Atomare Producer- und Consumer-Transaktionen verhindern verlorene beziehungsweise doppelte fachliche Effekte.
- Erster persistierter Progression-Vertical-Slice für atomare Experience-Point-Vergaben.
- Progression-eigene PostgreSQL-Migration und nebenläufigkeitssichere XP-Akkumulation.
- Minimale Progression-Grundlage für nicht-negative Experience Points.
- Community-bezogener Progressionszustand auf Basis der internen `CommunityIdentityId`.
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

### Geändert

- Der bestehende Engagement-zu-Progression-Workflow kann nun außerhalb von Tests kontinuierlich durch einen eigenen Worker-Host verarbeitet werden.
