# BuildingBlocks

`FlurNetz.BuildingBlocks` enthält kleine, domain-neutrale Primitives, die voraussichtlich von mehreren späteren Modulen gemeinsam benötigt werden. Der Inhalt bleibt bewusst minimal und unabhängig von Fachmodulen, Persistence, Messaging und Hosts.

## Erlaubte Inhalte

- fachlich neutrale Result-/Error-Typen
- generische technische Guards
- kleine technische Abstraktionen wie `IClock` sowie die neutrale `SystemClock`

## Nicht erlaubte Inhalte

- fachliche Modelle, Error-Codes oder Geschäftsregeln
- fachliche Services oder Modul-APIs
- Generic Repositories, Entity-Basisklassen oder Aggregate-Basisklassen
- Persistence-, Messaging-, API-, Worker- oder Integrationsimplementierungen

## Kriterien für neue Typen

Ein neuer Typ gehört nur dann in BuildingBlocks, wenn er fachlich neutral ist, von mehreren unabhängigen Modulen benötigt werden kann und keine Abhängigkeit auf ein konkretes Modul oder eine Infrastruktur einführt. Andernfalls bleibt er im jeweils zuständigen Modul oder Infrastrukturprojekt.
