# Identity

## Verantwortung

`FlurNetz.Modules.Identity` besitzt die zentrale interne Identität eines Community-Mitglieds.
Andere Module verwenden später ausschließlich diese interne Identität und nicht die Kennung
einer externen Plattform.

Die vorgesehene Trennung lautet:

`External Identity` → `Resolution/Mapping` → `CommunityIdentityId`

Twitch-, Discord-, YouTube- und Streamer.bot-Kennungen bleiben damit Integrations- oder
Mappingdaten und ersetzen niemals die interne FlurNetz-Identität.

## Aktueller Vertical-Slice-Stand

`FlurNetz.Modules.Identity.Contracts` bildet die öffentliche Grenze und enthält derzeit
ausschließlich `CommunityIdentityId`. Der unveränderliche Value Type basiert auf `Guid`,
weist `Guid.Empty` zurück und kann für neue interne Identitäten über `New()` erzeugt werden.

`FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity`. Sie
trägt ausschließlich eine gültige `CommunityIdentityId`; ihre ID kann nach der Erzeugung nicht
verändert werden. Der Use Case `CreateCommunityIdentity` erzeugt eine neue interne ID, bildet
damit eine Domain-Identität und persistiert sie. Die Identität kann anschließend über den
moduleigenen Repository-Port geladen werden.

Der Dapper-/Npgsql-Adapter liegt innerhalb der Identity-Implementierung und verwendet die
technische `PostgreSqlTransaction`. Die erste fachliche Migration gehört Identity und legt
`community_identities` mit genau der UUID-Primärschlüsselspalte `id` an. Die Migration wird
über den bestehenden SQL-first Migration Runner ausgeführt und in dessen History unter
`Identity:1:CreateCommunityIdentities` nachverfolgt.

`Identity.Contracts` bleibt bewusst auf den öffentlichen Identifier begrenzt. Der
Persistenz-Port, der Use Case, der Adapter und die Migrationsquelle sind keine öffentlichen
Cross-Module-Verträge. Andere Module verwenden künftig die interne `CommunityIdentityId`;
externe Plattformkennungen werden erst an einer späteren Resolution-/Mapping-Grenze
zugeordnet.

## HTTP-Adapter

Der bestehende `CreateCommunityIdentity`-Use-Case ist jetzt über den API-Adapter
`POST /api/identities` erreichbar. Der Endpunkt erzeugt keine eigene Identität, sondern
ruft ausschließlich den Use Case auf; die HTTP-Grenze übersetzt dessen
`CommunityIdentityId.Value` in ein API-Response-DTO. Details zur Host-Konfiguration und zum
Response-Vertrag stehen in [api.md](api.md).

Plattformkonten, Authentifizierung, Profile, fachliche Domain- oder Integration Events und
Messaging-/Outbox-Runtime bleiben weiterhin außerhalb dieses Slices.
