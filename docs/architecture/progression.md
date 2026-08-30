# Progression Vertical Slice

Progression besitzt den fachlichen Fortschritt einer internen Community-Identität. Der
erste persistierte Vertical Slice vergibt Experience Points über `GrantExperience` und
speichert den neuen Gesamtwert dauerhaft in PostgreSQL. Progression verwendet dafür
ausschließlich die bestehende `CommunityIdentityId` aus
`FlurNetz.Modules.Identity.Contracts` und führt keine eigene Benutzer-ID ein.

## Domain und Use Case

`ExperiencePoints` ist ein unveränderlicher, auf `long` basierender Value Type. Zero ist
gültig, negative Werte sind verboten und eine Addition oberhalb von `long.MaxValue` wird
als `OverflowException` sichtbar abgelehnt. `CommunityProgression` gehört genau einer
gültigen `CommunityIdentityId`, startet bei `0` XP und kann positive XP-Vergaben
akkumulieren.

`Create(...)` bedeutet fachlich einen neuen Zustand bei `0` XP. `Rehydrate(...)`
rekonstruiert ausschließlich einen bereits gespeicherten Zustand; beide Wege haben keine
öffentlichen Setter und umgehen die Domain-Invarianten nicht.

`GrantExperience` bleibt ein kleiner interner Application Use Case. Er erhält
`CommunityIdentityId` und den XP-Betrag, delegiert die atomare Persistenzmutation und
liefert den neuen Gesamtwert nach erfolgreichem Commit zurück. Einen separaten
`CreateCommunityProgression`-Use-Case gibt es nicht: Eine Progression entsteht lazy bei
der ersten erfolgreichen XP-Vergabe.

## Persistence und Nebenläufigkeit

`ICommunityProgressionStore` ist ein gezielter, interner Persistence-Port. Seine
Grant-Operation bietet bewusst keinen getrennten Get+Save-Pfad an. Der PostgreSQL-
Adapter führt Initialisierung, Sperren, Rehydration, Domain-Mutation, Update und Commit
in derselben `PostgreSqlTransaction` aus:

1. Fehlende Zustände werden mit `INSERT ... ON CONFLICT (community_identity_id) DO NOTHING`
   bei `0` XP initialisiert. `ON CONFLICT` wird ausschließlich für diese lazy Initialisierung
   verwendet und überschreibt keinen XP-Gesamtwert.
2. Die Zeile wird mit `SELECT ... FOR UPDATE` gesperrt.
3. Die Domain wird rehydriert und `CommunityProgression.GrantExperience(...)` berechnet
   den neuen Wert.
4. Der von der Domain gelieferte Gesamtwert wird aktualisiert und gemeinsam committet.

Dadurch werden parallele erste Vergaben und parallele spätere Vergaben auf PostgreSQL-
Ebene serialisiert; ungeschützte Lost Updates und In-Memory-Locks sind nicht Teil des
Designs. Rollback bei ungültigem Betrag, Overflow, Cancellation oder Datenbankfehler
entfernt auch eine eventuell nur innerhalb der fehlgeschlagenen Transaktion angelegte
`0`-XP-Zeile.

Die Migration `Progression:1:CreateCommunityProgressions` legt die Tabelle
`community_progressions` mit ausschließlich `community_identity_id uuid PRIMARY KEY`
und `experience_points bigint NOT NULL` an. Ein `CHECK (experience_points >= 0)` bildet
zusätzliche Defense in Depth. Die CommunityIdentityId ist ein fachlicher Identifier über
die Modulgrenze; es gibt bewusst keinen Foreign Key auf `community_identities` und keine
Identity-Datenbankabfrage.

Die PostgreSQL-Integrationstests prüfen Migration und Idempotenz, lazy First Grant,
Folgevergaben, Laden, Not-Found, Rollback, Overflow, den Datenbank-Check, die fehlende
Identity-Tabelle sowie 20 parallele erste und 20 parallele spätere Vergaben.

`FlurNetz.Modules.Progression.Contracts` bleibt bewusst leer, weil Progression für den Consumer
keinen eigenen öffentlichen Vertrag benötigt. Persistence-Port, Use Case, Adapter, Migration
und Consumer bleiben in der Implementierungs-Assembly.

## Messaging-Consumer

Progression konsumiert `MessageEngagementRecordedIntegrationEvent` aus
`FlurNetz.Modules.Engagement.Contracts`. Das Event ist eine fachliche Tatsache, kein Command;
Progression interpretiert es mit der ausschließlich hier definierten Policy:
`MessageExperiencePoints = 1`. Engagement kennt diese Regel nicht.

Der Consumer besitzt die stabile Inbox-Identität `progression.message-engagement-xp`. Er
rekonstruiert `CommunityIdentityId` über `CommunityIdentityId.Create(...)` und führt den Grant
über `CommunityProgression.GrantExperience(1)` aus. Es gibt keinen Identity-Lookup und keine
Referenz auf `FlurNetz.Modules.Engagement`.

Der transaction-aware Overload von `ICommunityProgressionStore` nimmt nur `DbConnection` und
`DbTransaction` entgegen. Der Handler reicht `IntegrationEventHandlerContext.Connection` und
`.Transaction` aus der Inbox-Zustellung durch. Damit verwendet der Consumer den gemeinsamen
Grant-Kern, ohne eine zweite unabhängige PostgreSQL-Transaktion zu öffnen:

`Inbox INSERT → Initialize → SELECT FOR UPDATE → Rehydrate → Domain Grant → UPDATE → COMMIT`

Schlägt die Domain-Mutation fehl, werden Inbox-Eintrag und XP-Änderung gemeinsam zurückgerollt.
Die bestehende Outbox-/Inbox-Retry-Semantik bleibt zuständig. Duplicate Delivery wird allein
über die Messaging-Inbox erkannt; Progression führt keine eigene Dedup-Tabelle ein.

Progression veröffentlicht in diesem Schritt kein neues Integration Event. Noch nicht
Bestandteil sind Level oder Level-Berechnung, Rewards, Economy, API-Endpunkte und ein dauerhaft
laufender Worker- oder Background-Processor-Host.
