# Rewards Foundation

## Verantwortung

Das Rewards-Modul beschreibt, was eine konfigurierte Belohnung fachlich bedeutet. Es
definiert Reward Definitions, fasst sie in verpflichtenden Reward Packages zusammen und
führt die fachliche Herkunft sowie spätere Grant-Records. Rewards besitzt dabei niemals den
resultierenden Zustand eines anderen Moduls und verändert ihn nicht selbst.

Die Foundation ist bewusst domain-only. Es gibt noch keinen Application Use Case, keine
Persistence und keine Ausführung.

## Reward Definitions

`RewardDefinition` ist die minimale abstrakte Basis einer Definition und besitzt ausschließlich
die interne `RewardDefinitionId`. `RewardDefinitionId`, `RewardPackageId` und `RewardGrantId`
sind getrennte, unveränderliche, Guid-basierte Fachtypen. Leere GUIDs werden abgelehnt; es gibt
keine gemeinsame generische ID-Infrastruktur.

Der erste und einzige konkrete Zieltyp dieser Foundation ist
`EconomyBalanceRewardDefinition`. Er beschreibt eine spätere Gutschrift eines positiven
`long Amount` auf einen Economy-Saldo. Der Typ benennt keine konkrete Währung und referenziert
weder Economy noch `Economy.Contracts`. Rewards beschreibt nur die gewünschte Wirkung; ob der
Zielsaldo die Gutschrift einschließlich seiner eigenen Overflow-Regel anwenden kann, entscheidet
später Economy.

XP ist ausdrücklich kein Reward-Komponententyp. Die bestehende Regel „Message → 1 XP“ bleibt
vollständig im Progression-Modul.

## Reward Packages

`RewardPackage` ist eine fachliche Zusammenfassung von mindestens einer gültigen
`RewardDefinitionId`. Die Sicht auf die Definitionen ist unveränderlich. Leere IDs und doppelte
Definitionen sind nicht erlaubt. Die Collection kann technisch eine stabile Reihenfolge
beibehalten, verspricht aber keine fachliche Ausführungsreihenfolge.

Ein Package beschreibt eine verpflichtende Menge von Reward Definitions. Bei der späteren V1-
Ausführung gilt daher: Entweder sind alle Komponenten erfolgreich oder keine. Eine optionale,
auswählbare, zufällige oder gewichtete Package-Komponente ist nicht Teil dieser Foundation.

## RewardSource

`RewardSource` beschreibt die fachliche Ursache eines Grants mit `SourceType` und `SourceId`.
Beide Werte müssen vorhanden, nicht leer und nicht aus Whitespace bestehen. `SourceType` bleibt
bewusst ein String und wird nicht als Rewards-eigenes Enum fest verdrahtet. So können spätere
Quell-Domänen wie Achievements, Progression, Daily oder Administration neue stabile Source Types
liefern, ohne das Rewards-Domainmodell zu ändern.

Event-, Message- und Inbox-IDs gehören nicht in `RewardSource`; technische Deduplizierung kommt
erst mit der späteren Messaging-Ausführung.

## RewardGrant

`RewardGrant` ist der fachliche Record einer einzelnen Reward-Definition-Ausführung. Er enthält
genau eine `RewardDefinitionId`, die zentrale Empfängeridentität `CommunityIdentityId` und die
gültige `RewardSource` neben seiner `RewardGrantId`. Eine zusätzliche `RewardPackageId` ist in
diesem Schritt nicht erforderlich.

Die spätere verbindliche Grant-Eindeutigkeit lautet:

```text
SourceType + SourceId + RewardDefinitionId
```

Diese Regel ist hier nur fachlich dokumentiert. Es gibt noch keine Datenbank-Constraint, keinen
Repository-Lookup und keinen globalen `HashSet`.

Ein Grant enthält noch keinen Status und keinen Zeitstempel. Ein erfolgreicher Grant-Record wird
erst im späteren Ausführungs-Slice relevant; eine fehlgeschlagene Ausführung erzeugt keinen
erfolgreichen Grant.

## Modulgrenzen und bewusste Ausschlüsse

`CommunityIdentityId` aus `FlurNetz.Modules.Identity.Contracts` ist die einzige verwendete
Empfängeridentität. Rewards führt keine zweite Benutzer-ID ein.

`FlurNetz.Modules.Rewards.Contracts` bleibt bewusst leer, weil noch kein echter öffentlicher
Cross-Module-Contract begründet ist. Die Domain-Typen bleiben im Implementierungsprojekt.
Inventory- und Title-Rewards werden erst modelliert, wenn die jeweiligen Zielmodule fachlich
existieren.

Nicht Bestandteil dieses Schritts sind:

- tatsächliche Economy-Ausführung oder eine Economy-Abhängigkeit
- XP-Reward-Typen oder eine Progression-Abhängigkeit
- Inventory- oder Title-Definitionen
- Persistence, Repository, Store, SQL, Migration oder Tabelle
- Messaging, Events, Inbox oder Outbox
- Application Use Cases, Modulregistrierung, API oder Worker-Anbindung

Die erlaubte Projektabhängigkeit der Implementierung ist neben dem leeren eigenen Contracts-
Projekt ausschließlich `FlurNetz.Modules.Identity.Contracts`. Es werden keine neuen NuGet-
Pakete eingeführt.
