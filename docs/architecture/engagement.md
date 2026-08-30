# Engagement

## Verantwortung

`FlurNetz.Modules.Engagement` bildet künftig normalisierte Community-Aktivitäten ab. Das
Modul arbeitet dabei ausschließlich mit einer bereits aufgelösten internen
`CommunityIdentityId`.

Der verbindliche Ablauf lautet:

`Externe Plattformidentität` → `Identity Resolution` → `CommunityIdentityId` → `Engagement`

Externe Plattformkennungen sind deshalb weder die zentrale Benutzeridentität noch Teil der
aktuellen Engagement-Domain.

## Foundation

Die Implementierung enthält zunächst nur:

- `EngagementActivityId` als unveränderlichen, modulinternen Identifier auf Basis einer nicht leeren
  `Guid`;
- `EngagementActivity` mit `EngagementActivityId Id` und `CommunityIdentityId CommunityIdentityId`.

`EngagementActivityId` liegt bewusst in `FlurNetz.Modules.Engagement` und nicht in
`FlurNetz.Modules.Engagement.Contracts`, weil aktuell kein anderes Modul diese Kennung benötigt.
`Engagement.Contracts` bleibt daher bewusst leer.

Die Foundation legt noch keinen Activity-Type-Katalog fest. Zeitpunkt, konkrete Aktivitätsart
und weitere Daten entstehen erst mit dem ersten realen Recording-Use-Case.

## Bewusst nicht enthalten

Dieser Stand enthält noch:

- keine Persistence, Tabellen, Migration oder Repositories;
- kein Messaging, keine Domain- oder Integration Events und keine Outbox;
- keine Progression-Kommunikation und keine XP-, Coin-, Reward- oder Item-Logik;
- keinen Recording-Use-Case und keine API-Erweiterung;
- keine Twitch-, Discord-, YouTube-, Kick- oder Streamer.bot-Integration.

Die einzige neue Cross-Module-Abhängigkeit der Engagement-Implementierung ist
`FlurNetz.Modules.Identity.Contracts`. Die Identity-Implementierung wird nicht referenziert.
