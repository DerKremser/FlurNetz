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

`FlurNetz.Modules.Progression.Contracts` bleibt bewusst leer, weil dieser Slice noch
keinen realen Cross-Module-Vertrag benötigt. Persistence-Port, Use Case, Adapter und
Migration bleiben in der Implementierungs-Assembly.

Noch nicht Bestandteil dieses Slices sind Messaging, Engagement-Kommunikation,
Integration- oder Domain-Events, Level oder Level-Berechnung, Rewards und API-Endpunkte.
Der spätere Engagement→Progression-Workflow wird separat über Messaging geplant.
