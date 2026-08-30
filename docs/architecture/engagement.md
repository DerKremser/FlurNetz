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

`EngagementActivityId` und die Persistence-Ports bleiben in der Implementierungs-Assembly.
`FlurNetz.Modules.Engagement.Contracts` enthält jetzt ausschließlich das erste öffentliche
Integration Event dieses Moduls.

## Integration Event und Producer-Atomicity

Nach erfolgreicher Domain-Erzeugung erstellt `RecordMessageEngagement` ein
`MessageEngagementRecordedIntegrationEvent`. Der Contract liegt bewusst in Engagement, weil
Engagement die fachliche Tatsache besitzt: „Eine normalisierte Message-Aktivität wurde
aufgezeichnet.“ Der stabile logische Typ ist `engagement.message-recorded`, die Schema-Version
ist `1`. Die Payload enthält ausschließlich die interne `CommunityIdentityId` als `Guid`.

Das Event enthält weder XP noch eine Progressionsanweisung, Reward-, Level-, Coin- oder
Plattformdaten. `EngagementActivityId` bleibt modulintern. Der Envelope verwendet exakt den
`EngagementActivity.OccurredAtUtc`-Zeitpunkt, erzeugt eine eigene nicht leere technische
`MessageId` und lässt `CorrelationId` sowie `CausationId` leer, solange keine echte Korrelation
existiert. Das Event ist eine Tatsache und kein Command zur XP-Vergabe.

Der interne `IMessageEngagementRecorder` garantiert über
`PostgreSqlMessageEngagementRecorder`, dass Activity-INSERT und Outbox-INSERT des bestehenden
`PostgreSqlOutboxPublisher` in derselben `PostgreSqlTransaction` liegen. Erst der gemeinsame
Commit macht beide Writes dauerhaft sichtbar; ein Publish nach einem separaten Activity-Commit
findet nicht statt. Der Recorder verwendet den bestehenden transaction-aware
`EngagementActivityRepository`-Pfad und dupliziert kein Activity-SQL.

Die Registry wird explizit mit Eventtyp, Message Type und Schema-Version komponiert. Es gibt
kein Assembly Scanning und kein Messaging- oder Progressionswissen in der technischen
Messaging-Foundation.

## Bewusst nicht enthalten

Dieser Stand enthält noch:

- keine Domain Events, keine automatische XP-Regel und keine Progressionsimplementierung;
- keine Level-, Coin-, Reward- oder Item-Logik;
- keine API-Erweiterung und keinen öffentlichen HTTP-Recording-Endpunkt;
- keine Twitch-, Discord-, YouTube-, Kick- oder Streamer.bot-Integration.

Engagement veröffentlicht das Event, ruft Progression aber nicht direkt auf und kennt die
fachliche Regel „Message → 1 XP“ nicht. Der Outbox-Processor wird in diesem Schritt noch nicht
als dauerhaft laufender Host betrieben.

Der Message-Slice ist intern über Use Case, Repository, Migration und Modulregistrierung
vollständig funktionsfähig. Weitere Aktivitätstypen werden erst mit konkretem fachlichem Bedarf
eingeführt.
