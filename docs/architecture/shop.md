# Shop Foundation

## Verantwortung

Das Modul `FlurNetz.Modules.Shop` beschreibt das fachliche Angebot eines Shops. Dieser erste
Slice enthält ausschließlich die interne Angebotsdomain und den stabilen öffentlichen
Angebots-Identifier. Er enthält keine Käufe, keine Identitäten, keine Economy-Anbindung und
keine Bestandsoperation.

## Öffentliche Contracts

`FlurNetz.Modules.Shop.Contracts` enthält ausschließlich `ShopOfferId`. Der immutable,
Guid-basierte Fachtyp weist `Guid.Empty` zurück und bildet die stabile öffentliche ID eines
Shop-Angebots. `ShopPurchaseId`, `ShopPurchaseRequestId`, Commands, Queries und Events gehören
nicht zu diesem Slice.

`ItemDefinitionId` wird nicht dupliziert. Das Shop-Modul verwendet genau den bestehenden Typ aus
`FlurNetz.Modules.Inventory.Contracts` als gemeinsame Ziel-Item-ID.

## Domain

`ShopPrice` ist ein unveränderlicher, auf `long` basierender Preiswert. Null ist für kostenlose
Angebote zulässig; negative Beträge werden abgelehnt.

`AvailabilityWindow` beschreibt ein optional begrenztes Zeitfenster. Der Beginn ist inklusive,
das Ende exklusiv; die Semantik lautet `[AvailableFrom, AvailableUntil)`. Beide Grenzen dürfen
fehlen. Sind beide gesetzt, muss der Beginn vor dem Ende liegen. Die Verfügbarkeitsprüfung
erhält ihren Zeitpunkt als Parameter und liest keine Systemzeit.

`ShopOffer` enthält `ShopOfferId`, `ItemDefinitionId`, einen kanonisch getrimmten
`DisplayName` mit 1 bis 200 Zeichen, eine optionale nicht-leere `Description` mit höchstens
2000 Zeichen, `ShopPrice`, `IsEnabled`, ein `AvailabilityWindow` und das optionale positive
`PurchaseLimitPerIdentity`. Ein neues Angebot startet immer deaktiviert.

Shop-Angebots-ID und Ziel-Item-ID sind nach der Erstellung unveränderlich. DisplayName,
Description, Preis, Availability und Kauflimit werden nur über gezielte Domainmethoden geändert;
`Enable` und `Disable` steuern den Aktivierungszustand. Es gibt keine frei beschreibbaren
öffentlichen Setter und keine generische `OfferTarget`-Abstraktion.

## Abhängigkeiten und bewusste Ausschlüsse

`FlurNetz.Modules.Shop` referenziert ausschließlich `FlurNetz.Modules.Shop.Contracts` und
`FlurNetz.Modules.Inventory.Contracts`. `FlurNetz.Modules.Shop.Contracts` referenziert keine
anderen FlurNetz-Projekte. Shop referenziert in diesem Slice ausdrücklich weder
`Identity.Contracts`, `Economy.Contracts`, `Persistence`, `Messaging`, `Administration`, `API`,
`Rewards` noch eine fremde Modulimplementierung.

Nicht enthalten sind Shop-Persistenz, Migrationen, `shop_offers`, Käufe, Economy-/Coin-Anbindung,
Inventory Grant, transaction-aware Inventory-Capabilities, Messaging, Domain- oder Integration
Events, Outbox, Inbox, API, Administration, globales `ShopEnabled`, Kategorien, Sortierung,
Featured-Angebote, Bilder, Rabatte, Stock, Warenkorb, Refunds sowie Titles-/Rewards-Ziele.

## Tests

Die Shop-Unit-Tests prüfen Identifier, Preise, alle vier Zeitfenster-Kombinationen inklusive
Grenzsemantik, Angebotsinvarianten, Textgrenzen, Kauflimits, gezielte Mutationen, Aktivierung,
Deaktivierung und die Unveränderlichkeit des Angebotsziels. Architekturtests sichern die
minimale Contracts-Assembly, die erlaubten Shop-Referenzen sowie das Fehlen von Purchase-,
Persistence-, Messaging-, Economy-, Administration- und API-Typen ab.
