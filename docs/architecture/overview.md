# Architekturübersicht

FlurNetz wird modular aufgebaut. Die physischen Grenzen der vorgesehenen Fachmodule sind jetzt als getrennte Contracts- und Implementierungs-Assemblies angelegt. Identity besitzt als erstes Modul einen bewusst minimalen fachlichen Vertical Slice; die übrigen Fachmodule enthalten noch keine Fachlogik, fachlichen Entities, Tabellen oder konkreten Events. Mit `FlurNetz.Api` besitzt FlurNetz erstmals einen ausführbaren Host. Fachmodule greifen nicht auf fremde Implementierungen oder Tabellen zu; eine spätere Cross-Module-Kommunikation erfolgt über öffentliche Contracts und Events.

Identity bildet mit `CommunityIdentityId` und der minimalen `CommunityIdentity` die zentrale interne Identität eines Community-Mitglieds. Der erste Slice erzeugt diese Identität, persistiert sie in der Identity-eigenen PostgreSQL-Tabelle und lädt sie über die interne ID. Der aktuelle End-to-End-Weg lautet `FlurNetz.Api` → `FlurNetz.Modules.Identity` → `FlurNetz.Persistence` → PostgreSQL. Externe Plattformkennungen werden später an Integrationsgrenzen aufgelöst und ersetzen diese interne Identität nicht. Persistence und Messaging werden als getrennte technische Infrastruktur aufgebaut; externe Systeme werden über Adapter integriert.

Streamer.bot wird später als externer Adapter behandelt und lädt keine internen FlurNetz-Assemblies. Interne FlurNetz-Projekte verwenden .NET 10. PostgreSQL ist die primäre relationale Datenbank; die technische Grundlage dafür liegt in `FlurNetz.Persistence` mit Npgsql und Dapper.

Die technische Messaging Foundation ist jetzt in `FlurNetz.Messaging` implementiert. Sie trennt interne Domain Events von Integration Events, bietet einen In-Process-Dispatcher sowie eine PostgreSQL-Outbox und Inbox mit Retry, Poison-Status und Deduplizierung. Identity veröffentlicht im ersten Slice bewusst keine fachlichen Events und verwendet Messaging nicht. Die übrigen Fachmodule enthalten weiterhin keine fachlichen Entitäten, Tabellen oder Implementierungen. Details stehen in [messaging.md](messaging.md).

`FlurNetz.BuildingBlocks` ist bewusst minimal gehalten und enthält ausschließlich domain-neutrale Primitives. Es gibt dort keine fachlichen Modelle, Generic Repositories oder fachlichen Services. Die Architekturtests sichern die heute prüfbaren Projekt- und Namespace-Grenzen automatisiert ab.

Die Regeln für die Aufnahme weiterer gemeinsamer Bausteine sind in [building-blocks.md](building-blocks.md) festgehalten.

Die Persistence Foundation stellt einen SQL-first Migration Runner und eine technische Migration-History bereit. Spätere Fachmodule liefern ihre Migrationen selbst und bleiben Eigentümer ihrer fachlichen Tabellen. Die technischen Grenzen und Konventionen sind in [persistence.md](persistence.md) beschrieben.

`FlurNetz.Messaging` darf auf BuildingBlocks und Persistence zeigen, nicht umgekehrt. Die Outbox verwendet die vorhandene Persistence-Transaktionskapselung; ein Host ruft den Processor später ausdrücklich auf. Es gibt weder einen Worker Host noch einen externen Message Broker.

Die Fachmodule verwenden jeweils das Muster `FlurNetz.Modules.<Module>.Contracts` und `FlurNetz.Modules.<Module>`. Die Implementierung darf nur ihr eigenes Contracts-Projekt und ausdrücklich erlaubte technische Infrastruktur referenzieren; fremde Modulimplementierungen sind ausgeschlossen. Identity ist das erste Referenzmodul mit einem bewusst begrenzten Vertical Slice. Die vollständige Modul-Liste und Umsetzungsreihenfolge stehen in [modules.md](modules.md).
