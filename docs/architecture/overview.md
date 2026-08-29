# Architekturübersicht

FlurNetz wird modular aufgebaut. Fachmodule werden klar voneinander getrennt und greifen nicht auf fremde Implementierungen oder Tabellen zu. Eine spätere Cross-Module-Kommunikation erfolgt über öffentliche Contracts und Events.

Identity bildet später die zentrale interne Benutzeridentität. Persistence und Messaging werden als getrennte technische Infrastruktur aufgebaut. Die API und ein möglicher Worker dienen als Composition Roots; externe Systeme werden über Adapter integriert.

Streamer.bot wird später als externer Adapter behandelt und lädt keine internen FlurNetz-Assemblies. Interne FlurNetz-Projekte verwenden .NET 10. PostgreSQL ist als primäre relationale Datenbank geplant.

Für zuverlässige spätere Integration Events sind Outbox und Inbox als technische Infrastruktur vorgesehen. In diesem Grundgerüst werden noch keine fachlichen Entitäten, Tabellen, Events, Module oder Infrastrukturimplementierungen definiert.
