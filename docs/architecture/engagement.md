# Engagement

## Verantwortung

`FlurNetz.Modules.Engagement` bildet normalisierte Community-Aktivitäten ab. Die erste
konkrete Aktivität ist `Message`: Eine bereits durch Identity aufgelöste interne
`CommunityIdentityId` hat eine Community-Nachricht erzeugt.

Der verbindliche Ablauf lautet:

`Externe Plattformidentität` → `Identity Resolution` → `CommunityIdentityId` → `Engagement`

Engagement prüft die Existenz der Identität nicht selbst und fragt keine Identity-Tabelle ab.
Die Auflösung ist vor dem Recording abgeschlossen. Externe Plattformkennungen sind weder die
zentrale Benutzeridentität noch Teil der Engagement-Domain.

## Erster Recording-Vertical-Slice

`RecordMessageEngagement` erhält ausschließlich die `CommunityIdentityId`. Der Use Case erzeugt
eine `EngagementActivityId`, bestimmt `OccurredAtUtc` über `IClock`, erstellt eine Message-
Aktivität und speichert sie über den Engagement-eigenen Persistenz-Port. Der UTC-Zeitpunkt und
die Aktivitätsart sind unveränderlich Teil der Domain-Entity.

`EngagementActivity` enthält aktuell genau:

- `EngagementActivityId Id`;
- `CommunityIdentityId CommunityIdentityId`;
- `EngagementActivityType Type` mit ausschließlich `Message`;
- `DateTimeOffset OccurredAtUtc` mit UTC-Offset.

Message speichert bewusst keinen Nachrichtentext, keine Message-ID, keinen Channel und keine
Emotes. Ebenso werden keine Plattform-, XP-, Coin- oder Reward-Daten übernommen.

## Persistenz

`EngagementActivityRepository` verwendet gezielte parametrisierte Dapper-SQLs und die vorhandene
`PostgreSqlTransaction`. Die Engagement-eigene Migration `Engagement:1:CreateEngagementActivities`
legt `engagement_activities` mit `id`, `community_identity_id`, `activity_type` und
`occurred_at_utc` an. Der Aktivitätstyp wird stabil als logischer Code `message` gespeichert;
Zeitpunkte werden als `timestamptz` persistiert.

`community_identity_id` ist ein fachlicher Cross-Module-Identifier. Die Engagement-Tabelle
besitzt deshalb bewusst keinen Foreign Key auf `community_identities` oder eine andere
Identity-Tabelle. Engagement besitzt seine Daten selbst.

`EngagementActivityId` und der Repository-Port bleiben in der Implementierungs-Assembly. Da
aktuell kein anderes Modul einen öffentlichen Engagement-Vertrag benötigt, bleibt
`FlurNetz.Modules.Engagement.Contracts` bewusst leer.

## Bewusst nicht enthalten

Dieser Stand enthält noch:

- keine Domain- oder Integration Events und keine Outbox;
- keine Progression-Kommunikation und keine XP-, Coin-, Reward- oder Item-Logik;
- keine API-Erweiterung und keinen öffentlichen HTTP-Recording-Endpunkt;
- keine Twitch-, Discord-, YouTube-, Kick- oder Streamer.bot-Integration.

Der Message-Slice ist intern über Use Case, Repository, Migration und Modulregistrierung
vollständig funktionsfähig. Weitere Aktivitätstypen werden erst mit konkretem fachlichem Bedarf
eingeführt.
