# FlurNetz

FlurNetz ist ein neues, modular aufgebautes .NET-Projekt. Der aktuelle Stand ist ein technisches Repository- und Solution-Grundgerüst für die frühe Entwicklung; fachliche Module und Infrastrukturimplementierungen sind noch nicht enthalten.

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

## Lokale Entwicklung

Voraussetzung ist das in `global.json` festgelegte stabile .NET-10-SDK.

```text
dotnet restore
dotnet build
dotnet test
```

Die initiale Architektur-Richtung ist in [docs/architecture/overview.md](docs/architecture/overview.md) beschrieben.
