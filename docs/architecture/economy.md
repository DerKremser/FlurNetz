# Economy-Foundation

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

`FlurNetz.Modules.Economy.Contracts` bleibt leer, weil dieser Foundation-Schritt
noch keinen echten Cross-Module-Vertrag benötigt. Es gibt noch keine
Persistence, SQL-Migration, Repository oder Store, keinen Application Use Case,
kein Messaging, keine Inbox/Outbox und keine Domain- oder Integration Events.
Ebenso sind noch keine Transfers, Rewards, kein Shop und keine API-Anbindung
Bestandteil des Moduls. Der Worker bleibt unverändert.

Die einzige fachfremde Referenz der Economy-Implementierung ist aktuell
`FlurNetz.Modules.Identity.Contracts`. Eine Referenz auf Identity-Implementierung,
Engagement, Progression, Rewards, Shop, Inventory, Messaging oder Persistence
existiert nicht.
