# FlurNetz

FlurNetz ist ein neues, modular aufgebautes .NET-Projekt. Der aktuelle Stand enthält neben dem technischen Repository- und Solution-Grundgerüst erstmals eine minimale BuildingBlocks-Grundlage sowie erste automatisierte Architekturtests. Fachmodule und Infrastrukturimplementierungen sind noch nicht enthalten.

## Technische Basis

- .NET 10 für interne FlurNetz-Projekte
- C# 14
- modulare Architektur mit klarer Trennung der Fachmodule
- `System.Text.Json` als Standard für JSON
- `Microsoft.Extensions.Logging` als Logging-Basis
- PostgreSQL als geplante primäre relationale Datenbank
- Dapper und Npgsql als geplante Datenzugriffsbasis
- xUnit v3 für die technische Testgrundlage

PostgreSQL, Dapper, Npgsql und weitere fachliche oder infrastrukturelle Bausteine werden erst in späteren Arbeitsschritten hinzugefügt.

## BuildingBlocks und Architekturtests

`FlurNetz.BuildingBlocks` enthält ausschließlich kleine, domain-neutrale Primitives für eine spätere gemeinsame Nutzung. Dazu gehören Result-/Error-Typen, generische Guards und die minimale `IClock`-Abstraktion.

Die Projekte `FlurNetz.BuildingBlocks.Tests` und `FlurNetz.Architecture.Tests` prüfen das Verhalten dieser Primitives sowie grundlegende Projekt-, Namespace- und Typgrenzen. Fachmodule, Persistence, Messaging, API und Worker sind weiterhin nicht Bestandteil des Projekts.

## Lokale Entwicklung

Voraussetzung ist das in `global.json` festgelegte stabile .NET-10-SDK.

```text
dotnet restore
dotnet build
dotnet test
```

Die initiale Architektur-Richtung ist in [docs/architecture/overview.md](docs/architecture/overview.md) beschrieben. Die Regeln für BuildingBlocks stehen in [docs/architecture/building-blocks.md](docs/architecture/building-blocks.md).
