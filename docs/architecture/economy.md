# Economy-Vertical-Slice

## Verantwortung

Das Modul `FlurNetz.Modules.Economy` besitzt den monetären beziehungsweise
community-bezogenen Economy-Zustand einer internen `CommunityIdentityId`. Im
aktuellen Modell existiert genau ein Economy-Saldo je `CommunityIdentityId`.
Economy verwendet dafür die zentrale Identität aus
`FlurNetz.Modules.Identity.Contracts`; es führt keine zweite Benutzerkennung ein
und kennt keine externen Plattformidentitäten.

Die Foundation enthält ausschließlich den nicht-negativen Saldo sowie fachlich
gültiges Gutschreiben und Abbuchen. XP, Level, Engagement-Aktivitäten, Rewards,
Items, Shop-Produkte und Transfers gehören nicht zu Economy.

## EconomyBalance

`EconomyBalance` ist ein unveränderlicher Value Type auf Basis von `long`. Er
modelliert kleinste ganzzahlige Economy-Einheiten. `EconomyBalance.Zero` ist ein
gültiger Wert; `EconomyBalance.Create(long value)` akzeptiert null und positive
Werte und lehnt negative Werte ab.

Eine Gutschrift über `Credit(long amount)` benötigt einen positiven Betrag. Null
und negative Beträge sind keine Gutschriften und werden als ungültige
Methodenargumente abgelehnt. Eine Addition oberhalb von `long.MaxValue` wird
sichtbar als `OverflowException` abgelehnt; es gibt keinen Wraparound oder
Clamp.

Eine Abbuchung über `Debit(long amount)` benötigt ebenfalls einen positiven
Betrag. Reicht der Saldo nicht aus, wird der fachlich gültige, aber nicht
ausführbare Vorgang durch `InsufficientEconomyBalanceException` abgelehnt. Der
Saldo kann dadurch niemals negativ werden. Eine Abbuchung bis exakt null ist
zulässig.

## CommunityEconomy

`CommunityEconomy.Create(CommunityIdentityId)` erzeugt einen Zustand mit der
angegebenen, nicht leeren `CommunityIdentityId` und dem Initialsaldo
`EconomyBalance.Zero`. Die Identität ist unveränderlich. `Balance` kann nur über
die fachlichen Methoden `Credit` und `Debit` verändert werden; die Entity besitzt
keine öffentliche parameterlose Konstruktion.

Die Methoden delegieren die Wertlogik an `EconomyBalance`. Da ein
`EconomyBalance` immutable ist, verändert ein fehlgeschlagener Vorgang den
ursprünglichen Wert nicht. Die Entity enthält neben den fachlichen Methoden
ausschließlich `CommunityIdentityId` und `Balance`.

## Persistenz und Lebenszyklus

Der erste persistierte Economy-Zustand entsteht lazy mit der ersten erfolgreichen
Gutschrift. Dafür legt `CreditEconomyBalance` keine eigene Create-Operation an,
sondern delegiert an `ICommunityEconomyStore`. Der Store initialisiert eine
fehlende Zeile innerhalb derselben Transaktion mit einem temporären Saldo von
0, sperrt sie und führt anschließend `CommunityEconomy.Credit` aus. Erst der
fachlich berechnete neue Saldo wird gespeichert und committed.

`CommunityEconomy.Create` bedeutet weiterhin ausschließlich einen neuen
fachlichen Zustand mit Saldo null. `CommunityEconomy.Rehydrate` rekonstruiert
dagegen einen bereits gespeicherten, zuvor validierten Saldo. Beide Wege bleiben
getrennt; die Domain kennt weder Dapper noch PostgreSQL.

`DebitEconomyBalance` liest den Zustand innerhalb derselben Transaktion mit
`SELECT FOR UPDATE`. Fehlt die Zeile, wird fachlich ein verfügbarer Saldo von
null behandelt. `CommunityEconomy.Debit` wirft dann
`InsufficientEconomyBalanceException`; die Transaktion rollt zurück und es wird
keine Economy-Zeile angelegt. Bei einer vorhandenen Zeile wird sie rehydriert,
mutiert und aktualisiert. Eine erfolgreiche Abbuchung bis exakt null lässt die
Zeile bestehen.

`Credit` und `Debit` sind damit jeweils vollständige atomare
Read/Modify/Write-Operationen. PostgreSQL ist die Concurrency-Grenze; es gibt
keine In-Memory-Sperre. Parallele Credits sperren dieselbe Zeile nach der lazy
Initialisierung nacheinander, parallele Debits werden durch dieselbe
Zeilensperre und die Domain-Invariante korrekt begrenzt.

Die interne Persistenzgrenze ist `ICommunityEconomyStore` mit den normalen
`CreditAsync`, `DebitAsync` und `GetByCommunityIdentityIdAsync`-Operationen sowie einem
transaction-aware `CreditAsync`-Overload. Der Overload nimmt nur `DbConnection` und
`DbTransaction` entgegen, führt keinen Commit aus und verwendet exakt denselben
Read/Modify/Write-Kern wie der normale Credit-Pfad. Die beiden internen Application Use
Cases `CreditEconomyBalance` und `DebitEconomyBalance` enthalten weder SQL noch
Transaktionssteuerung.

Die Migration `Economy:1:CreateCommunityEconomies` gehört dem Economy-Modul und
legt `community_economies` mit exakt den Spalten
`community_identity_id uuid PRIMARY KEY` und `balance bigint NOT NULL` an.
Ein `CHECK (balance >= 0)` schützt zusätzlich zur Domain vor korrupten negativen
Zuständen. `CommunityIdentityId` ist zugleich Primärschlüssel, weil aktuell genau
ein Economy-Zustand je Community existiert. Es gibt bewusst keinen Foreign Key
auf `community_identities`; die ID bleibt ein fachlicher Cross-Module-Identifier.

Der PostgreSQL-Adapter verwendet ausschließlich parametrisiertes Dapper-SQL.
Es gibt keine Identity-Abfrage, keine Zeitspalten, keine Economy-ID und kein
Ledger oder History-Modell.

## Bewusst nicht vorweggenommen

Der technische und fachliche Saldo wird zunächst neutral modelliert. Eine
sichtbare Währungsbezeichnung ist Produkt- und Use-Case-Semantik und wird erst
festgelegt, wenn ein realer Anwendungsfall sie benötigt. So wird keine spätere
Migration durch eine vorschnelle Namenswahl erzwungen. Die Methode `Credit`
bezeichnet hier ausschließlich das fachliche Gutschreiben und ist kein
Währungsname.

Ebenso gibt es aktuell keine `Currency`-, Currency-Code- oder sonstige
Multi-Currency-Struktur. Das Modell besitzt genau einen Saldo pro
`CommunityIdentityId`; mehrere Währungen werden erst bei nachgewiesenem
fachlichem Bedarf bewusst ergänzt.

`FlurNetz.Modules.Economy.Contracts` enthält nun ausschließlich die schmale,
Rewards-neutrale Fähigkeit `IEconomyBalanceCredit`. Sie nimmt die zentrale
`CommunityIdentityId`, einen positiven Betrag sowie neutrale `DbConnection`- und
`DbTransaction`-Parameter entgegen. Dadurch kann ein aufrufendes Modul seine fachlichen
Writes und die Economy-Gutschrift in exakt derselben Transaktion koordinieren. Economy kennt
den Aufrufer Rewards nicht; der Adapter delegiert weiterhin an den bestehenden Store und
damit an dieselbe Domain- und SQL-Logik.

Es gibt weiterhin kein Messaging, keine Inbox/Outbox und keine Domain- oder Integration
Events. Ebenso sind noch keine Transfers, Rewards-Trigger, kein Shop und keine API-Anbindung
Bestandteil des Economy-Moduls. Der Worker bleibt unverändert.

Die fachfremden Referenzen der Economy-Implementierung sind
`FlurNetz.Modules.Identity.Contracts` und `FlurNetz.Persistence`. Eine Referenz
auf die Identity-Implementierung, Engagement, Progression, Rewards, Shop,
Inventory oder Messaging existiert nicht.
