# Identity

## Verantwortung

`FlurNetz.Modules.Identity` besitzt die zentrale interne Identität eines Community-Mitglieds.
Andere Module verwenden später ausschließlich diese interne Identität und nicht die Kennung
einer externen Plattform.

Die vorgesehene Trennung lautet:

`External Identity` → `Resolution/Mapping` → `CommunityIdentityId`

Twitch-, Discord-, YouTube- und Streamer.bot-Kennungen bleiben damit Integrations- oder
Mappingdaten und ersetzen niemals die interne FlurNetz-Identität.

## Foundation-Stand

`FlurNetz.Modules.Identity.Contracts` bildet die öffentliche Grenze und enthält derzeit
ausschließlich `CommunityIdentityId`. Der unveränderliche Value Type basiert auf `Guid`,
weist `Guid.Empty` zurück und kann für neue interne Identitäten über `New()` erzeugt werden.

`FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity`. Sie
trägt ausschließlich eine gültige `CommunityIdentityId`; ihre ID kann nach der Erzeugung nicht
verändert werden. Die Implementierung referenziert nur das eigene Contracts-Projekt.

Diese Foundation ist noch kein vollständiger Identity-Use-Case. Es gibt derzeit keine
Persistenz, keine fachliche Migration, kein Repository, keine Plattformkonten, keine
Authentifizierung, keine API und keine fachlichen Domain- oder Integration Events.
