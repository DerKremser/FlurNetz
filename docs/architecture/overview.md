# Architekturübersicht

FlurNetz wird modular aufgebaut. Fachmodule werden klar voneinander getrennt und greifen nicht auf fremde Implementierungen oder Tabellen zu. Eine spätere Cross-Module-Kommunikation erfolgt über öffentliche Contracts und Events.

Identity bildet später die zentrale interne Benutzeridentität. Persistence und Messaging werden als getrennte technische Infrastruktur aufgebaut. Die API und ein möglicher Worker dienen als Composition Roots; externe Systeme werden über Adapter integriert.

Streamer.bot wird später als externer Adapter behandelt und lädt keine internen FlurNetz-Assemblies. Interne FlurNetz-Projekte verwenden .NET 10. PostgreSQL ist die primäre relationale Datenbank; die technische Grundlage dafür liegt in `FlurNetz.Persistence` mit Npgsql und Dapper.

Für zuverlässige spätere Integration Events sind Outbox und Inbox als technische Infrastruktur vorgesehen, aber noch nicht implementiert. Fachliche Entitäten, Tabellen, Events und Module sind weiterhin nicht Bestandteil des Projekts.

`FlurNetz.BuildingBlocks` ist bewusst minimal gehalten und enthält ausschließlich domain-neutrale Primitives. Es gibt dort keine fachlichen Modelle, Generic Repositories oder fachlichen Services. Die Architekturtests sichern die heute prüfbaren Projekt- und Namespace-Grenzen automatisiert ab.

Die Regeln für die Aufnahme weiterer gemeinsamer Bausteine sind in [building-blocks.md](building-blocks.md) festgehalten.

Die Persistence Foundation stellt einen SQL-first Migration Runner und eine technische Migration-History bereit. Spätere Fachmodule liefern ihre Migrationen selbst und bleiben Eigentümer ihrer fachlichen Tabellen. Die technischen Grenzen und Konventionen sind in [persistence.md](persistence.md) beschrieben.
