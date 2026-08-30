# Architekturübersicht

FlurNetz wird modular aufgebaut. Die physischen Grenzen der vorgesehenen Fachmodule sind jetzt als getrennte Contracts- und Implementierungs-Assemblies angelegt. Identity ist das erste Modul mit einem bewusst minimalen fachlichen Vertical Slice und einer API; Engagement kann eine normalisierte Message-Aktivität gemeinsam mit einer Outbox-Nachricht persistieren; Progression konsumiert diese Nachricht und vergibt atomar 1 XP; Economy persistiert atomare Community-Salden mit PostgreSQL-Zeilensperren. Die übrigen noch nicht begonnenen Fachmodule enthalten keine Fachlogik, fachlichen Entities, Tabellen oder konkreten Events. Mit `FlurNetz.Api` und `FlurNetz.Worker` besitzt FlurNetz zwei unabhängige ausführbare Hosts. Fachmodule greifen nicht auf fremde Implementierungen oder Tabellen zu; Cross-Module-Kommunikation erfolgt über öffentliche Contracts und Integration Events.

Identity bildet mit `CommunityIdentityId` und der minimalen `CommunityIdentity` die zentrale interne Identität eines Community-Mitglieds. Der erste Slice erzeugt diese Identität, persistiert sie in der Identity-eigenen PostgreSQL-Tabelle und lädt sie über die interne ID. Engagement nimmt eine bereits aufgelöste `CommunityIdentityId` an und persistiert intern eine Message-Aktivität mit UTC-Zeitpunkt; es fragt Identity nicht ab und verwendet keinen Cross-Module-Foreign-Key. Externe Plattformkennungen werden an Integrationsgrenzen aufgelöst und ersetzen die interne Identität nicht. Persistence und Messaging werden als getrennte technische Infrastruktur aufgebaut; externe Systeme werden über Adapter integriert.

Progression hält mit `CommunityProgression` den aktuellen XP-Wert einer einzelnen
`CommunityIdentityId`. Der erste persistierte Slice startet mit `0` XP, erzeugt den Zustand
lazy beim ersten Grant und unterstützt positive, überlaufsichere Akkumulation. Die atomare
Read/Modify/Write-Operation verwendet PostgreSQL-Zeilensperren gegen Lost Updates. Der erste
Engagement→Progression-Workflow läuft über Outbox und Inbox; weder Level-Logik noch
Rewards oder API gehören zu diesem Stand.

Economy hält mit `CommunityEconomy` den neutralen Economy-Saldo genau einer internen
`CommunityIdentityId`. `EconomyBalance` ist immutable, auf `long` basierend und nicht-negativ;
Gutschriften und Abbuchungen akzeptieren ausschließlich positive Beträge, schützen vor Overflow
und verhindern eine Überziehung. Der Zustand wird lazy bei der ersten erfolgreichen Gutschrift
angelegt. Der interne Store führt Credits und Debits atomar mit `SELECT FOR UPDATE` aus; ein
fehlgeschlagener Debit auf einen fehlenden Zustand erzeugt keine Zeile. Eine konkrete
Währungsbezeichnung, Multi-Currency, Messaging, Events, Transfers, Rewards, Shop und API sind
noch nicht Bestandteil dieses Economy-Slices.

Streamer.bot wird später als externer Adapter behandelt und lädt keine internen FlurNetz-Assemblies. Interne FlurNetz-Projekte verwenden .NET 10. PostgreSQL ist die primäre relationale Datenbank; die technische Grundlage dafür liegt in `FlurNetz.Persistence` mit Npgsql und Dapper.

Die technische Messaging Foundation ist jetzt in `FlurNetz.Messaging` implementiert. Sie trennt interne Domain Events von Integration Events, bietet einen In-Process-Dispatcher sowie eine PostgreSQL-Outbox und Inbox mit Retry, Poison-Status und Deduplizierung. Der erste reale Einsatz führt Engagement-Aktivität und Outbox atomar zusammen und verarbeitet das Event über `FlurNetz.Worker`, `OutboxProcessor` und den Progression-Consumer. Die Foundation bleibt fachlich neutral und referenziert kein Modul; der Worker ist eine separate Composition Root. Details stehen in [messaging.md](messaging.md) und [worker.md](worker.md).

`FlurNetz.BuildingBlocks` ist bewusst minimal gehalten und enthält ausschließlich domain-neutrale Primitives. Es gibt dort keine fachlichen Modelle, Generic Repositories oder fachlichen Services. Die Architekturtests sichern die heute prüfbaren Projekt- und Namespace-Grenzen automatisiert ab.

Die Regeln für die Aufnahme weiterer gemeinsamer Bausteine sind in [building-blocks.md](building-blocks.md) festgehalten.

Die Persistence Foundation stellt einen SQL-first Migration Runner und eine technische Migration-History bereit. Spätere Fachmodule liefern ihre Migrationen selbst und bleiben Eigentümer ihrer fachlichen Tabellen. Identity, Engagement, Progression und Economy besitzen jeweils eigene fachliche Migrationen; Progression und Economy verwenden für ihre atomaren Mutationen PostgreSQL-Zeilensperren. Die technischen Grenzen und Konventionen sind in [persistence.md](persistence.md) beschrieben.

`FlurNetz.Messaging` darf auf BuildingBlocks und Persistence zeigen, nicht umgekehrt. Die Outbox verwendet die vorhandene Persistence-Transaktionskapselung; die unabhängigen Hosts rufen den Processor ausdrücklich auf. Es gibt keinen externen Message Broker.

Die Fachmodule verwenden jeweils das Muster `FlurNetz.Modules.<Module>.Contracts` und `FlurNetz.Modules.<Module>`. Die Implementierung darf nur ihr eigenes Contracts-Projekt und ausdrücklich erlaubte technische Infrastruktur sowie öffentliche Cross-Module-Contracts referenzieren; Engagement verwendet zusätzlich `Identity.Contracts`, Persistence und Messaging, um Activity und Outbox atomar zu speichern. Progression verwendet zusätzlich `Identity.Contracts`, Persistence, Messaging und ausschließlich `Engagement.Contracts`, um das Event als `1 XP` zu interpretieren. Economy verwendet zusätzlich ausschließlich `Identity.Contracts` und besitzt noch keine technische Infrastruktur oder öffentliche fachliche Verträge. Fremde Modulimplementierungen sind ausgeschlossen. Identity bleibt das erste Referenzmodul; Engagement veröffentlicht jetzt die erste fachliche Integration-Nachricht und Progression ist der erste Consumer. Der E2E-Workflow ist implementiert, getestet und wird durch den unabhängigen Worker-Host dauerhaft betrieben. Die vollständige Modul-Liste und Umsetzungsreihenfolge stehen in [modules.md](modules.md).
